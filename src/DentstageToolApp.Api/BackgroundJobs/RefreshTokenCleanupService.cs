using System;
using System.Linq;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.BackgroundJobs;

/// <summary>
/// 週期性清理過期 Refresh Token 的背景服務，維持資料庫整潔。
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// 建構子，注入必要的相依物件。
    /// </summary>
    public RefreshTokenCleanupService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 服務啟動時先進行一次清理，避免長期累積
        await CleanupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // 取消時直接跳出迴圈，避免噴出不必要的錯誤
                break;
            }
        }
    }

    /// <summary>
    /// 實際執行清理的流程。
    /// </summary>
    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();
        var now = DateTime.UtcNow;

        // ---------- 資料查詢區 ----------
        // 找出所有過期或已撤銷超過七天的 Token
        var expiredTokens = await context.RefreshTokens
            .Where(x => x.ExpireAt < now || (x.IsRevoked && x.RevokedAt != null && x.RevokedAt < now.AddDays(-7)))
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count == 0)
        {
            _logger.LogDebug("Refresh Token 清理結果：無過期資料。");
            return;
        }

        // ---------- 方法區 ----------
        // 批次移除過期資料，減少資料庫負擔
        context.RefreshTokens.RemoveRange(expiredTokens);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh Token 清理完成，共移除 {Count} 筆資料。", expiredTokens.Count);
    }
}
