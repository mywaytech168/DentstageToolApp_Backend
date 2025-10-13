using System;
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
    /// 執行單次門市同步流程：更新狀態並統計待處理資料量。
    /// </summary>
    private async Task RunSyncCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();
            var now = DateTime.UtcNow;

            // ---------- 取得或建立門市同步狀態 ----------
            var storeState = await dbContext.StoreSyncStates.FirstOrDefaultAsync(
                state => state.StoreId == _syncOptions.StoreId && state.StoreType == _syncOptions.StoreType,
                cancellationToken);

            if (storeState is null)
            {
                storeState = new StoreSyncState
                {
                    StoreId = _syncOptions.StoreId!,
                    StoreType = _syncOptions.StoreType!,
                    LastUploadTime = now,
                    LastDownloadTime = now
                };
                await dbContext.StoreSyncStates.AddAsync(storeState, cancellationToken);
            }
            else
            {
                storeState.LastUploadTime = now;
                storeState.LastDownloadTime = now;
            }

            // ---------- 統計待同步筆數，提供監控參考 ----------
            var pendingCount = await dbContext.SyncLogs.CountAsync(
                log => !log.Synced && log.StoreType == _syncOptions.StoreType,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "完成門市同步排程，StoreId: {StoreId}, StoreType: {StoreType}, 未同步筆數: {PendingCount}, 執行時間: {Time:O}",
                _syncOptions.StoreId,
                _syncOptions.StoreType,
                pendingCount,
                now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "門市同步排程執行失敗，StoreId: {StoreId}, StoreType: {StoreType}", _syncOptions.StoreId, _syncOptions.StoreType);
        }
    }
}
