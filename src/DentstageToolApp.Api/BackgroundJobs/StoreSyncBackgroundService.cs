using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Api.Services.Sync;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.BackgroundJobs;

/// <summary>
/// 門市環境使用的同步背景服務，固定時間呼叫同步流程。
/// </summary>
public class StoreSyncBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // ---------- 讓中央下發資料反序列化時可忽略大小寫差異 ----------
        PropertyNameCaseInsensitive = true
    };

    private const string PhotoBinaryField = "fileContentBase64";
    private const string PhotoExtensionField = "fileExtension";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StoreSyncBackgroundService> _logger;
    private readonly SyncOptions _syncOptions;
    private readonly IRemoteSyncApiClient _remoteSyncApiClient;
    private readonly PhotoStorageOptions _photoStorageOptions;

    /// <summary>
    /// 建構子，注入必要的相依物件。
    /// </summary>
    public StoreSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IRemoteSyncApiClient remoteSyncApiClient,
        IOptions<SyncOptions> syncOptions,
        IOptions<PhotoStorageOptions> photoStorageOptions,
        ILogger<StoreSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncOptions = syncOptions.Value ?? throw new ArgumentNullException(nameof(syncOptions));
        _remoteSyncApiClient = remoteSyncApiClient;
        _photoStorageOptions = photoStorageOptions?.Value ?? new PhotoStorageOptions();
    }

    /// <summary>
    /// 依據設定檔啟動同步排程，僅在門市角色時執行。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var normalizedRole = _syncOptions.NormalizedServerRole;
        if (SyncServerRoles.IsCentralRole(normalizedRole))
        {
            _logger.LogInformation("目前伺服器角色為 {Role}，不需啟動門市同步背景工作。", string.IsNullOrWhiteSpace(normalizedRole) ? "未設定" : normalizedRole);
            return;
        }

        if (!_syncOptions.HasResolvedMachineProfile)
        {
            _logger.LogWarning("伺服器角色為門市，但尚未透過同步機碼補齊 StoreId/Role 設定，請確認 UserAccounts 是否已設定 ServerRole 與 Role。");
            return;
        }

        var intervalMinutes = _syncOptions.BackgroundSyncIntervalMinutes <= 0
            ? 60
            : _syncOptions.BackgroundSyncIntervalMinutes;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        _logger.LogInformation("門市同步背景工作已啟動，將每 {Interval} 分鐘執行一次。", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            // ---------- 先進行上傳流程，確保門市端的異動先行送出 ----------
            await RunSyncCycleAsync(stoppingToken);

            // ---------- 上傳完成後接續下載中央資料，避免資料落差 ----------
            await RunDownloadCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ---------- 停止訊號 ----------
                break;
            }
        }
    }

    /// <summary>
    /// 執行單次門市同步流程：補齊同步紀錄欄位並統計待處理資料量。
    /// </summary>
    private async Task RunSyncCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();

            UserAccount? machineAccount = null;
            if (!string.IsNullOrWhiteSpace(_syncOptions.MachineKey))
            {
                // ---------- 改以裝置註冊與使用者資料解析同步機碼 ----------
                var deviceRegistration = await dbContext.DeviceRegistrations
                    .AsNoTracking()
                    .Include(registration => registration.UserAccount)
                    .FirstOrDefaultAsync(registration => registration.DeviceKey == _syncOptions.MachineKey, cancellationToken);

                if (deviceRegistration is null)
                {
                    _logger.LogWarning("找不到同步機碼 {MachineKey} 對應的裝置註冊資料，停止本次背景同步。", _syncOptions.MachineKey);
                    return;
                }

                if (deviceRegistration.UserAccount is null)
                {
                    _logger.LogWarning("裝置註冊 {RegistrationUid} 缺少對應的使用者帳號，請確認 DeviceRegistrations.UserUID 設定。", deviceRegistration.DeviceRegistrationUid);
                    return;
                }

                if (string.IsNullOrWhiteSpace(deviceRegistration.UserAccount.ServerRole))
                {
                    _logger.LogWarning("使用者帳號 {UserUid} 尚未設定 ServerRole，無法判斷同步角色。", deviceRegistration.UserAccount.UserUid);
                    return;
                }

                machineAccount = deviceRegistration.UserAccount;

                // ---------- 若資料庫設定更新，立即同步到記憶體中的選項 ----------
                var accountStoreId = machineAccount.UserUid;
                var accountStoreType = machineAccount.Role;
                _syncOptions.ApplyMachineProfile(machineAccount.ServerRole, accountStoreId, accountStoreType);
            }

            var storeId = _syncOptions.StoreId ?? machineAccount?.UserUid ?? "UNKNOWN";
            var storeType = _syncOptions.StoreType ?? machineAccount?.Role ?? "UNKNOWN";
            var serverRole = _syncOptions.NormalizedServerRole;

            if (SyncServerRoles.IsCentralRole(serverRole))
            {
                _logger.LogInformation("同步機碼設定顯示目前角色為 {Role}，略過門市同步流程。", serverRole);
                return;
            }

            // ---------- 補齊待同步紀錄的來源資訊 ----------
            var batchSize = _syncOptions.BackgroundSyncBatchSize <= 0 ? 100 : _syncOptions.BackgroundSyncBatchSize;
            var pendingLogs = await dbContext.SyncLogs
                .Where(log => !log.Synced)
                .OrderBy(log => log.SyncedAt)
                .ThenBy(log => log.UpdatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (pendingLogs.Count == 0)
            {
                _logger.LogInformation("門市同步排程執行完畢，目前無待同步資料。StoreId: {StoreId}", storeId);
                return;
            }

            var metadataUpdated = false;

            foreach (var log in pendingLogs)
            {
                // ---------- 逐筆補齊來源伺服器與門市類型 ----------
                if (string.IsNullOrWhiteSpace(log.SourceServer))
                {
                    log.SourceServer = storeId;
                    metadataUpdated = true;
                }

                if (string.IsNullOrWhiteSpace(log.StoreType))
                {
                    log.StoreType = storeType;
                    metadataUpdated = true;
                }
            }

            if (metadataUpdated)
            {
                // ---------- 先保存補齊後的欄位，避免上傳失敗時資訊遺失 ----------
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var totalProcessed = 0;
            var totalIgnored = 0;
            var failureCount = 0;
            var hasSuccessfulUpload = false;

            foreach (var log in pendingLogs)
            {
                var change = new SyncChangeDto
                {
                    LogId = log.Id,
                    TableName = log.TableName,
                    Action = log.Action,
                    RecordId = log.RecordId,
                    UpdatedAt = log.UpdatedAt,
                    SyncedAt = log.SyncedAt
                };

                if (!string.Equals(log.Action, "DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = await BuildChangePayloadAsync(dbContext, log, cancellationToken);
                    if (payload.HasValue)
                    {
                        change.Payload = payload.Value;
                    }
                    else
                    {
                        // ---------- 若查無內容，仍送出 metadata 供中央判斷 ----------
                        _logger.LogWarning("同步紀錄 {Id} 缺少對應資料內容，將以純 metadata 上傳。", log.Id);
                    }
                }

                var singleRequest = new SyncUploadRequest
                {
                    StoreId = storeId,
                    StoreType = storeType,
                    ServerRole = serverRole,
                    ServerIp = _syncOptions.ServerIp,
                    Changes = new List<SyncChangeDto> { change }
                };

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var preview = JsonSerializer.Serialize(singleRequest, SerializerOptions);
                    _logger.LogDebug("門市同步逐筆預覽：{Preview}", preview);
                }

                try
                {
                    // ---------- 逐筆呼叫中央 API，確保單筆成功才會標記同步 ----------
                    var uploadResult = await _remoteSyncApiClient.UploadChangesAsync(singleRequest, cancellationToken);
                    if (uploadResult is null)
                    {
                        failureCount++;
                        _logger.LogWarning("中央同步 API 回傳空值，保留同步紀錄 {Id} 以便下次重試。", log.Id);
                        continue;
                    }

                    log.Synced = true;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    totalProcessed += uploadResult.ProcessedCount;
                    totalIgnored += uploadResult.IgnoredCount;
                    hasSuccessfulUpload = true;
                }
                catch (Exception ex)
                {
                    // ---------- 保留未標記同步的紀錄，下次週期將再次嘗試 ----------
                    failureCount++;
                    _logger.LogError(ex, "上傳同步紀錄 {Id} 失敗，將於下次排程重試。", log.Id);
                }
            }

            if (!hasSuccessfulUpload)
            {
                _logger.LogWarning("門市同步排程未成功上傳任何資料，StoreId: {StoreId}, StoreType: {StoreType}, 失敗筆數: {FailureCount}", storeId, storeType, failureCount);
                return;
            }

            var storeAccount = await dbContext.UserAccounts
                .FirstOrDefaultAsync(account => account.UserUid == storeId, cancellationToken);

            if (storeAccount is null)
            {
                _logger.LogWarning("找不到門市 {StoreId} 對應的使用者帳號，無法更新同步狀態資訊。", storeId);
            }
            else
            {
                storeAccount.ServerRole = serverRole;
                if (!string.IsNullOrWhiteSpace(storeType))
                {
                    storeAccount.Role = storeType;
                }
                if (!string.IsNullOrWhiteSpace(_syncOptions.ServerIp))
                {
                    storeAccount.ServerIp = _syncOptions.ServerIp;
                }

                storeAccount.LastUploadTime = DateTime.UtcNow;
                storeAccount.LastSyncCount = totalProcessed;

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var pendingCount = await dbContext.SyncLogs.CountAsync(log => !log.Synced, cancellationToken);

            _logger.LogInformation(
                "完成門市同步排程，StoreId: {StoreId}, StoreType: {StoreType}, 成功筆數: {Processed}, 忽略筆數: {Ignored}, 失敗筆數: {FailureCount}, 未同步總筆數: {PendingCount}",
                storeId,
                storeType,
                totalProcessed,
                totalIgnored,
                failureCount,
                pendingCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "門市同步排程執行失敗，StoreId: {StoreId}, StoreType: {StoreType}", _syncOptions.StoreId, _syncOptions.StoreType);
        }
    }

    /// <summary>
    /// 依據同步紀錄產生對應的資料內容，支援 orders 表的差異打包。
    /// </summary>
    private async Task<JsonElement?> BuildChangePayloadAsync(
        DentstageToolAppContext dbContext,
        SyncLog log,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(log.Payload))
        {
            try
            {
                // ---------- 優先使用同步紀錄保存的 Payload，確保資料刪除後仍能上傳 ----------
                using var document = JsonDocument.Parse(log.Payload);
                return document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "同步紀錄 {Id} 的 Payload 格式錯誤，改以資料庫內容補齊。", log.Id);
            }
        }

        if (string.Equals(log.TableName, "orders", StringComparison.OrdinalIgnoreCase))
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.OrderUid == log.RecordId, cancellationToken);

            if (order is null)
            {
                return null;
            }

            var dto = new OrderSyncDto
            {
                OrderUid = order.OrderUid,
                OrderNo = order.OrderNo,
                StoreUid = order.StoreUid,
                Amount = order.Amount,
                Status = order.Status,
                CreationTimestamp = order.CreationTimestamp,
                ModificationTimestamp = order.ModificationTimestamp,
                QuatationUid = order.QuatationUid,
                CreatedBy = order.CreatedBy,
                ModifiedBy = order.ModifiedBy
            };

            return JsonSerializer.SerializeToElement(dto, SerializerOptions);
        }

        if (string.Equals(log.TableName, "photo_data", StringComparison.OrdinalIgnoreCase))
        {
            return await BuildPhotoPayloadAsync(dbContext, log.RecordId, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// 將照片資料打包為同步 Payload，並附帶實體檔案的 Base64 字串。
    /// </summary>
    private async Task<JsonElement?> BuildPhotoPayloadAsync(
        DentstageToolAppContext dbContext,
        string photoUid,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(photoUid))
        {
            return null;
        }

        var photo = await dbContext.PhotoData
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.PhotoUid == photoUid, cancellationToken);

        if (photo is null)
        {
            _logger.LogWarning("找不到照片 {PhotoUid} 的資料庫紀錄，無法建立同步 Payload。", photoUid);
            return null;
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["photoUid"] = photo.PhotoUid,
            ["quotationUid"] = photo.QuotationUid,
            ["relatedUid"] = photo.RelatedUid,
            ["posion"] = photo.Posion,
            ["comment"] = photo.Comment,
            ["photoShape"] = photo.PhotoShape,
            ["photoShapeOther"] = photo.PhotoShapeOther,
            ["photoShapeShow"] = photo.PhotoShapeShow,
            ["cost"] = photo.Cost,
            ["flagFinish"] = photo.FlagFinish,
            ["finishCost"] = photo.FinishCost
        };

        var storageRoot = EnsurePhotoStorageRoot();
        var physicalPath = ResolvePhotoPhysicalPath(storageRoot, photo.PhotoUid);

        if (File.Exists(physicalPath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(physicalPath, cancellationToken);
                payload[PhotoBinaryField] = Convert.ToBase64String(bytes);

                var extension = Path.GetExtension(physicalPath);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    payload[PhotoExtensionField] = extension;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "讀取照片檔案 {PhotoUid} 失敗，將僅同步資料欄位。", photo.PhotoUid);
            }
        }
        else
        {
            _logger.LogWarning("找不到照片 {PhotoUid} 的實體檔案：{Path}", photo.PhotoUid, physicalPath);
        }

        return JsonSerializer.SerializeToElement(payload, SerializerOptions);
    }

    /// <summary>
    /// 下載中央伺服器的差異資料並套用至本地資料庫。
    /// </summary>
    private async Task RunDownloadCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();

            var storeId = _syncOptions.StoreId;
            var storeType = _syncOptions.StoreType;
            var serverRole = _syncOptions.NormalizedServerRole;

            if (string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(storeType))
            {
                _logger.LogWarning("中央下發流程缺少 StoreId 或 StoreType 設定，無法執行資料下載。");
                return;
            }

            var storeAccount = await dbContext.UserAccounts
                .FirstOrDefaultAsync(account => account.UserUid == storeId, cancellationToken);

            if (storeAccount is null)
            {
                _logger.LogWarning("找不到門市 {StoreId} 對應的使用者帳號，無法執行中央下發流程。", storeId);
                return;
            }

            var query = new SyncDownloadQuery
            {
                // ---------- 目前僅需提供最後同步時間，其餘識別資訊改由 Token 取得 ----------
                LastSyncTime = storeAccount.LastDownloadTime
            };

            var response = await _remoteSyncApiClient.GetUpdatesAsync(query, cancellationToken);
            if (response is null)
            {
                _logger.LogWarning("中央下發流程呼叫中央 API 失敗，StoreId: {StoreId}, StoreType: {StoreType}", storeId, storeType);
                return;
            }

            // ---------- 標記後續新增的同步紀錄皆來自中央，方便資料清理並帶入伺服器角色 ----------
            dbContext.SetSyncLogMetadata(storeId, storeType, serverRole);

            var changes = response.Changes ?? new List<SyncChangeDto>();
            if (changes.Count == 0 && response.Orders.Count == 0)
            {
                await UpdateDownloadStateAsync(dbContext, storeAccount, storeType, serverRole, response.ServerTime, 0, cancellationToken);
                await MarkCentralLogsAsSyncedAsync(dbContext, storeId, cancellationToken);
                _logger.LogInformation("中央下發流程完成，本次無差異資料。StoreId: {StoreId}", storeId);
                return;
            }

            var processedCount = 0;

            if (changes.Count > 0)
            {
                dbContext.DisableSyncLogAutoAppend();
                try
                {
                    var changeLogIds = changes
                        .Where(change => change.LogId.HasValue)
                        .Select(change => change.LogId!.Value)
                        .Distinct()
                        .ToList();

                    var existedLogIds = changeLogIds.Count == 0
                        ? new List<Guid>()
                        : await dbContext.SyncLogs
                            .AsNoTracking()
                            .Where(log => changeLogIds.Contains(log.Id))
                            .Select(log => log.Id)
                            .ToListAsync(cancellationToken);

                    var knownLogIds = new HashSet<Guid>(existedLogIds);

                    foreach (var change in changes)
                    {
                        if (change.LogId.HasValue && knownLogIds.Contains(change.LogId.Value))
                        {
                            // ---------- 若本地端已存在相同 LogId，代表曾處理過該筆異動，直接略過 ----------
                            _logger.LogInformation("門市資料庫已存在同步紀錄 {LogId}，略過重複套用。", change.LogId.Value);
                            continue;
                        }

                        if (change.LogId.HasValue)
                        {
                            knownLogIds.Add(change.LogId.Value);
                        }

                        await UpsertLocalSyncLogAsync(dbContext, change, storeId, storeType, response.ServerTime, cancellationToken);
                        await ApplyChangeAsync(dbContext, change, cancellationToken);
                        processedCount++;
                    }
                }
                finally
                {
                    dbContext.EnableSyncLogAutoAppend();
                }
            }
            else
            {
                // ---------- 沒有提供通用異動格式時，仍以工單同步方式處理 ----------
                foreach (var orderDto in response.Orders)
                {
                    var order = await dbContext.Orders
                        .FirstOrDefaultAsync(entity => entity.OrderUid == orderDto.OrderUid, cancellationToken);

                    if (order is null)
                    {
                        order = new Order
                        {
                            OrderUid = orderDto.OrderUid
                        };

                        dbContext.Orders.Add(order);
                    }

                    ApplyOrderSyncData(order, orderDto);
                }

                processedCount = response.Orders.Count;
            }
            await UpdateDownloadStateAsync(dbContext, storeAccount, storeType, serverRole, response.ServerTime, processedCount, cancellationToken);

            await MarkCentralLogsAsSyncedAsync(dbContext, storeId, cancellationToken);
            _logger.LogInformation("中央下發流程完成，StoreId: {StoreId}, 同步筆數: {Count}", storeId, processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "中央下發流程執行失敗，StoreId: {StoreId}, StoreType: {StoreType}", _syncOptions.StoreId, _syncOptions.StoreType);
        }
    }

    /// <summary>
    /// 套用中央傳回的異動資訊，依據動作類型更新本地資料。
    /// </summary>
    private async Task ApplyChangeAsync(DentstageToolAppContext dbContext, SyncChangeDto change, CancellationToken cancellationToken)
    {
        var action = change.Action?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(action))
        {
            _logger.LogWarning("中央下發資料缺少動作類型，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
            return;
        }

        var entityType = TryResolveEntityType(dbContext, change.TableName);
        if (entityType is null)
        {
            _logger.LogWarning("找不到資料表 {Table} 對應的實體，略過中央下發。", change.TableName);
            return;
        }

        if (!TryParseKeyValues(entityType, change.RecordId, out var keyValues))
        {
            _logger.LogWarning("解析中央下發紀錄主鍵失敗，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
            return;
        }

        switch (action)
        {
            case "INSERT":
            case "UPDATE":
            case "UPSERT":
                if (change.Payload is null)
                {
                    _logger.LogWarning("中央下發 {Action} 異動缺少 Payload，Table: {Table}, RecordId: {RecordId}", action, change.TableName, change.RecordId);
                    return;
                }

                object? payloadEntity;
                try
                {
                    payloadEntity = change.Payload.Value.Deserialize(entityType.ClrType, SerializerOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "中央下發 Payload 反序列化失敗，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
                    return;
                }

                if (payloadEntity is null)
                {
                    _logger.LogWarning("中央下發 Payload 反序列化結果為空，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
                    return;
                }

                var existedEntity = await dbContext.FindAsync(entityType.ClrType, keyValues);
                if (existedEntity is null)
                {
                    // ---------- 找不到舊資料時改為新增 ----------
                    dbContext.Add(payloadEntity);
                }
                else
                {
                    dbContext.Entry(existedEntity).CurrentValues.SetValues(payloadEntity);
                }

                break;

            case "DELETE":
                var entity = await dbContext.FindAsync(entityType.ClrType, keyValues);
                if (entity is not null)
                {
                    dbContext.Remove(entity);
                }

                break;

            default:
                _logger.LogWarning("中央下發資料包含未支援的動作：{Action}", action);
                break;
        }
    }

    /// <summary>
    /// 確保本地同步紀錄存在，若尚未建立則以中央提供的 LogId 與時間補齊，供後續重複檢查使用。
    /// </summary>
    private static async Task UpsertLocalSyncLogAsync(
        DentstageToolAppContext dbContext,
        SyncChangeDto change,
        string storeId,
        string storeType,
        DateTime serverTime,
        CancellationToken cancellationToken)
    {
        var logId = change.LogId ?? Guid.NewGuid();
        var action = change.Action?.Trim().ToUpperInvariant() ?? "UPDATE";
        var syncedAt = change.SyncedAt ?? change.UpdatedAt ?? serverTime;
        var updatedAt = change.UpdatedAt ?? syncedAt;
        var payloadJson = change.Payload.HasValue ? change.Payload.Value.GetRawText() : null;

        var existedLog = await dbContext.SyncLogs
            .FirstOrDefaultAsync(entity => entity.Id == logId, cancellationToken);

        if (existedLog is null)
        {
            existedLog = new SyncLog
            {
                Id = logId
            };

            await dbContext.SyncLogs.AddAsync(existedLog, cancellationToken);
        }

        // ---------- 將中央同步資訊完整寫回，供門市下次下載時能夠辨識已處理紀錄 ----------
        existedLog.TableName = change.TableName;
        existedLog.RecordId = change.RecordId;
        existedLog.Action = action;
        existedLog.UpdatedAt = updatedAt;
        existedLog.SyncedAt = syncedAt;
        existedLog.SourceServer = storeId;
        existedLog.StoreType = storeType;
        existedLog.Synced = true;
        existedLog.Payload = payloadJson;
    }

    /// <summary>
    /// 將中央回傳的工單資料套用至本地實體。
    /// </summary>
    private static void ApplyOrderSyncData(Order order, OrderSyncDto dto)
    {
        order.OrderNo = dto.OrderNo;
        order.StoreUid = dto.StoreUid;
        order.Amount = dto.Amount;
        order.Status = dto.Status;
        order.CreationTimestamp = dto.CreationTimestamp;
        order.ModificationTimestamp = dto.ModificationTimestamp;
        order.QuatationUid = dto.QuatationUid;
        order.CreatedBy = dto.CreatedBy;
        order.ModifiedBy = dto.ModifiedBy;
    }

    /// <summary>
    /// 更新門市同步狀態的下載資訊。
    /// </summary>
    private async Task UpdateDownloadStateAsync(
        DentstageToolAppContext dbContext,
        UserAccount storeAccount,
        string storeType,
        string serverRole,
        DateTime serverTime,
        int processedCount,
        CancellationToken cancellationToken)
    {
        if (storeAccount is null)
        {
            return;
        }

        // ---------- 同步中央狀態到使用者帳號，整併過往 StoreSyncStates 資訊 ----------
        storeAccount.ServerRole = serverRole;
        if (!string.IsNullOrWhiteSpace(storeType))
        {
            storeAccount.Role = storeType;
        }
        if (!string.IsNullOrWhiteSpace(_syncOptions.ServerIp))
        {
            storeAccount.ServerIp = _syncOptions.ServerIp;
        }

        storeAccount.LastDownloadTime = serverTime;
        storeAccount.LastSyncCount = processedCount;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 將中央來源的同步紀錄標記為已處理，避免重複下載。
    /// </summary>
    private static async Task MarkCentralLogsAsSyncedAsync(DentstageToolAppContext dbContext, string storeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeId))
        {
            return;
        }

        // ---------- 統一使用小寫來源清單，兼容舊有中央別名與最新的門市編號格式 ----------
        var normalizedStoreId = storeId.Trim();
        var acceptableSources = new[]
        {
            normalizedStoreId.ToLowerInvariant(),
            SyncServerRoles.CentralServer.ToLowerInvariant(),
            "Central".ToLowerInvariant()
        };

        var centralLogs = await dbContext.SyncLogs
            .Where(log => !log.Synced
                && !string.IsNullOrWhiteSpace(log.SourceServer)
                && acceptableSources.Contains(log.SourceServer!.ToLower()))
            .ToListAsync(cancellationToken);

        if (centralLogs.Count == 0)
        {
            return;
        }

        foreach (var log in centralLogs)
        {
            log.Synced = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 嘗試解析中央資料的主鍵內容，提供查詢所需的鍵值陣列。
    /// </summary>
    private bool TryParseKeyValues(IEntityType entityType, string? recordId, out object?[] keyValues)
    {
        keyValues = Array.Empty<object?>();
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
        {
            return false;
        }

        var segments = (recordId ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (segments.Length != primaryKey.Properties.Count)
        {
            return false;
        }

        keyValues = new object?[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            try
            {
                keyValues[index] = ConvertKeyValue(segments[index], primaryKey.Properties[index].ClrType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "中央下發主鍵值轉換失敗，Table: {Table}, Property: {Property}, Value: {Value}",
                    entityType.GetTableName(),
                    primaryKey.Properties[index].Name,
                    segments[index]);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 依資料表名稱尋找對應的實體資訊。
    /// </summary>
    private static IEntityType? TryResolveEntityType(DentstageToolAppContext dbContext, string tableName)
    {
        return dbContext.Model
            .GetEntityTypes()
            .FirstOrDefault(type => string.Equals(type.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 將文字型主鍵值轉換為實際型別，確保可套用至實體模型。
    /// </summary>
    private static object? ConvertKeyValue(string rawValue, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableType ?? targetType;

        if (actualType == typeof(string))
        {
            return rawValue;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return nullableType is null ? Activator.CreateInstance(actualType) : null;
        }

        if (actualType == typeof(Guid))
        {
            return Guid.Parse(rawValue);
        }

        if (actualType == typeof(DateTime))
        {
            return DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (actualType.IsEnum)
        {
            return Enum.Parse(actualType, rawValue, ignoreCase: true);
        }

        return Convert.ChangeType(rawValue, actualType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 取得照片儲存根目錄，若不存在則自動建立資料夾。
    /// </summary>
    private string EnsurePhotoStorageRoot()
    {
        var root = _photoStorageOptions.RootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "App_Data", "photos");
        }

        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
        }

        return root;
    }

    /// <summary>
    /// 依 PhotoUid 推導實際儲存的檔案路徑，支援不同副檔名。
    /// </summary>
    private static string ResolvePhotoPhysicalPath(string storageRoot, string photoUid)
    {
        var candidate = Directory.EnumerateFiles(storageRoot, photoUid + ".*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        return Path.Combine(storageRoot, photoUid);
    }
}
