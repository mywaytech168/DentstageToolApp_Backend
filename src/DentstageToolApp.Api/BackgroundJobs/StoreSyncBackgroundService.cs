using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Infrastructure.Data;
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

        if (string.IsNullOrWhiteSpace(_syncOptions.StoreId) || string.IsNullOrWhiteSpace(_syncOptions.StoreType))
        {
            _logger.LogWarning("伺服器角色為門市，但缺少 StoreId 或 StoreType 設定，請補齊設定後再啟動服務。");
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

            // ---------- 準備門市識別資訊，供同步紀錄補齊使用 ----------
            var storeId = _syncOptions.StoreId ?? _syncOptions.NormalizedServerRole ?? "UNKNOWN";
            var storeType = _syncOptions.StoreType ?? _syncOptions.NormalizedServerRole ?? "UNKNOWN";

            // ---------- 補齊待同步紀錄的來源資訊 ----------
            var batchSize = _syncOptions.BackgroundSyncBatchSize <= 0 ? 100 : _syncOptions.BackgroundSyncBatchSize;
            var pendingLogs = await dbContext.SyncLogs
                .Where(log => !log.Synced)
                .OrderBy(log => log.UpdatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var log in pendingLogs)
            {
                if (string.IsNullOrWhiteSpace(log.SourceServer))
                {
                    // 透過設定檔補齊來源伺服器，讓中央可辨識門市來源
                    log.SourceServer = storeId;
                }

                if (string.IsNullOrWhiteSpace(log.StoreType))
                {
                    // 透過設定檔補齊門市型態，中央可依類型決定處理邏輯
                    log.StoreType = storeType;
                }
            }

            if (pendingLogs.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var pendingCount = await dbContext.SyncLogs.CountAsync(log => !log.Synced, cancellationToken);

            _logger.LogInformation(
                "完成門市同步排程，StoreId: {StoreId}, StoreType: {StoreType}, 未同步筆數: {PendingCount}",
                storeId,
                storeType,
                pendingCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "門市同步排程執行失敗，StoreId: {StoreId}, StoreType: {StoreType}", _syncOptions.StoreId, _syncOptions.StoreType);
        }
    }
}
