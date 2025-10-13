using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
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

    /// <summary>
    /// 建構子，注入必要的相依物件。
    /// </summary>
    public StoreSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<SyncOptions> syncOptions,
        ILogger<StoreSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncOptions = syncOptions.Value ?? throw new ArgumentNullException(nameof(syncOptions));
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

            var changes = new List<SyncChangeDto>();

            foreach (var log in pendingLogs)
            {
                if (string.IsNullOrWhiteSpace(log.SourceServer))
                {
                    log.SourceServer = storeId;
                }

                if (string.IsNullOrWhiteSpace(log.StoreType))
                {
                    log.StoreType = storeType;
                }

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
                        _logger.LogWarning("同步紀錄 {Id} 缺少對應資料內容，將以純 metadata 上傳。", log.Id);
                    }
                }

                changes.Add(change);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var uploadRequest = new SyncUploadRequest
            {
                StoreId = storeId,
                StoreType = storeType,
                ServerRole = serverRole,
                ServerIp = _syncOptions.ServerIp,
                Changes = changes
            };

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var preview = JsonSerializer.Serialize(uploadRequest, SerializerOptions);
                _logger.LogDebug("門市同步預覽：{Preview}", preview);
            }

            var pendingCount = await dbContext.SyncLogs.CountAsync(log => !log.Synced, cancellationToken);

            _logger.LogInformation(
                "完成門市同步排程，StoreId: {StoreId}, StoreType: {StoreType}, 準備上傳筆數: {Prepared}, 未同步總筆數: {PendingCount}",
                storeId,
                storeType,
                changes.Count,
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
