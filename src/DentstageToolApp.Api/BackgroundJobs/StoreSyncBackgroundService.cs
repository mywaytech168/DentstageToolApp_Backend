using System;
using System.Collections.Generic;
using System.Globalization;
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

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StoreSyncBackgroundService> _logger;
    private readonly SyncOptions _syncOptions;
    private readonly IRemoteSyncApiClient _remoteSyncApiClient;

    /// <summary>
    /// 建構子，注入必要的相依物件。
    /// </summary>
    public StoreSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IRemoteSyncApiClient remoteSyncApiClient,
        IOptions<SyncOptions> syncOptions,
        ILogger<StoreSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncOptions = syncOptions.Value ?? throw new ArgumentNullException(nameof(syncOptions));
        _remoteSyncApiClient = remoteSyncApiClient;
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
            _logger.LogWarning("伺服器角色為門市，但尚未透過同步機碼補齊 StoreId/StoreType 設定，請確認 UserAccounts 是否已設定 ServerRole 與 Role。");
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
                _syncOptions.ApplyMachineProfile(machineAccount.ServerRole, machineAccount.UserUid, machineAccount.Role);
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
                .OrderBy(log => log.UpdatedAt)
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
                    TableName = log.TableName,
                    Action = log.Action,
                    RecordId = log.RecordId,
                    UpdatedAt = log.UpdatedAt
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

            var storeState = await dbContext.StoreSyncStates
                .FirstOrDefaultAsync(state => state.StoreId == storeId && state.StoreType == storeType, cancellationToken);

            if (storeState is null)
            {
                storeState = new StoreSyncState
                {
                    StoreId = storeId,
                    StoreType = storeType
                };

                dbContext.StoreSyncStates.Add(storeState);
            }

            storeState.ServerRole = serverRole;
            if (!string.IsNullOrWhiteSpace(_syncOptions.ServerIp))
            {
                storeState.ServerIp = _syncOptions.ServerIp;
            }

            storeState.LastUploadTime = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            await MarkStoreStateLogsAsSyncedAsync(dbContext, cancellationToken);

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
    /// 將同步狀態相關的同步紀錄標記為已處理，避免重複上傳。
    /// </summary>
    private static async Task MarkStoreStateLogsAsSyncedAsync(DentstageToolAppContext dbContext, CancellationToken cancellationToken)
    {
        var stateLogs = await dbContext.SyncLogs
            .Where(log => !log.Synced && string.Equals(log.TableName, "store_sync_states", StringComparison.OrdinalIgnoreCase))
            .ToListAsync(cancellationToken);

        if (stateLogs.Count == 0)
        {
            return;
        }

        foreach (var log in stateLogs)
        {
            log.Synced = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

        return null;
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

            var storeState = await dbContext.StoreSyncStates
                .FirstOrDefaultAsync(state => state.StoreId == storeId && state.StoreType == storeType, cancellationToken);

            var query = new SyncDownloadQuery
            {
                // ---------- 目前僅需提供最後同步時間，其餘識別資訊改由 Token 取得 ----------
                LastSyncTime = storeState?.LastDownloadTime
            };

            var response = await _remoteSyncApiClient.GetUpdatesAsync(query, cancellationToken);
            if (response is null)
            {
                _logger.LogWarning("中央下發流程呼叫中央 API 失敗，StoreId: {StoreId}, StoreType: {StoreType}", storeId, storeType);
                return;
            }

            // ---------- 標記後續新增的同步紀錄皆來自中央，方便資料清理 ----------
            dbContext.SetSyncLogMetadata(SyncServerRoles.CentralServer, storeType);

            var changes = response.Changes ?? new List<SyncChangeDto>();
            if (changes.Count == 0 && response.Orders.Count == 0)
            {
                await UpdateDownloadStateAsync(dbContext, storeState, storeId, storeType, serverRole, response.ServerTime, cancellationToken);
                await MarkCentralLogsAsSyncedAsync(dbContext, cancellationToken);
                _logger.LogInformation("中央下發流程完成，本次無差異資料。StoreId: {StoreId}", storeId);
                return;
            }

            if (changes.Count > 0)
            {
                foreach (var change in changes)
                {
                    await ApplyChangeAsync(dbContext, change, cancellationToken);
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
            }

            await UpdateDownloadStateAsync(dbContext, storeState, storeId, storeType, serverRole, response.ServerTime, cancellationToken);

            await MarkCentralLogsAsSyncedAsync(dbContext, cancellationToken);

            var processedCount = changes.Count > 0 ? changes.Count : response.Orders.Count;
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
        StoreSyncState? storeState,
        string storeId,
        string storeType,
        string serverRole,
        DateTime serverTime,
        CancellationToken cancellationToken)
    {
        storeState ??= new StoreSyncState
        {
            StoreId = storeId,
            StoreType = storeType
        };

        if (storeState.Id == 0)
        {
            dbContext.StoreSyncStates.Add(storeState);
        }

        storeState.ServerRole = serverRole;
        if (!string.IsNullOrWhiteSpace(_syncOptions.ServerIp))
        {
            storeState.ServerIp = _syncOptions.ServerIp;
        }

        storeState.LastDownloadTime = serverTime;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 將中央來源的同步紀錄標記為已處理，避免重複下載。
    /// </summary>
    private static async Task MarkCentralLogsAsSyncedAsync(DentstageToolAppContext dbContext, CancellationToken cancellationToken)
    {
        var centralLogs = await dbContext.SyncLogs
            .Where(log => !log.Synced && (log.SourceServer == SyncServerRoles.CentralServer
                || log.SourceServer == "Central"))
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
}
