using System;
using System.Collections.Generic;
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
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
        if (!SyncServerRoles.IsStoreRole(normalizedRole))
        {
            _logger.LogInformation("目前伺服器角色為 {Role}，不需啟動門市同步背景工作。", string.IsNullOrWhiteSpace(normalizedRole) ? "未設定" : normalizedRole);
            return;
        }

        if (!_syncOptions.HasResolvedMachineProfile)
        {
            _logger.LogWarning("伺服器角色為門市，但尚未透過同步機碼補齊 StoreId/StoreType 設定，請確認 SyncMachineProfiles。");
            return;
        }

        var intervalMinutes = _syncOptions.BackgroundSyncIntervalMinutes <= 0
            ? 60
            : _syncOptions.BackgroundSyncIntervalMinutes;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        _logger.LogInformation("門市同步背景工作已啟動，將每 {Interval} 分鐘執行一次。", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncCycleAsync(stoppingToken);

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

            SyncMachineProfile? machineProfile = null;
            if (!string.IsNullOrWhiteSpace(_syncOptions.MachineKey))
            {
                machineProfile = await dbContext.SyncMachineProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(profile => profile.MachineKey == _syncOptions.MachineKey && profile.IsActive, cancellationToken);

                if (machineProfile is null)
                {
                    _logger.LogWarning("找不到同步機碼 {MachineKey} 對應的設定，停止本次背景同步。", _syncOptions.MachineKey);
                    return;
                }

                // ---------- 若資料庫設定已更新，立即同步到記憶體中的選項 ----------
                _syncOptions.ApplyMachineProfile(machineProfile.ServerRole, machineProfile.StoreId, machineProfile.StoreType);
            }

            var storeId = _syncOptions.StoreId ?? machineProfile?.StoreId ?? "UNKNOWN";
            var storeType = _syncOptions.StoreType ?? machineProfile?.StoreType ?? "UNKNOWN";
            var serverRole = _syncOptions.NormalizedServerRole;

            if (!SyncServerRoles.IsStoreRole(serverRole))
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
    private static async Task<JsonElement?> BuildChangePayloadAsync(DentstageToolAppContext dbContext, SyncLog log, CancellationToken cancellationToken)
    {
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
}
