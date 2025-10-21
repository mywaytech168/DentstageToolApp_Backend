using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.BackgroundJobs;

/// <summary>
/// 週期性檢查估價單與維修單是否超過 60 天未處理，必要時自動標記為過期。
/// </summary>
public class QuotationMaintenanceExpirationService : BackgroundService
{
    private const string SchedulerOperator = "系統排程";
    private static readonly string[] TaipeiTimeZoneIds = { "Taipei Standard Time", "Asia/Taipei" };

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<QuotationMaintenanceExpirationService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    /// <summary>
    /// 建構子，注入背景排程所需的相依物件。
    /// </summary>
    public QuotationMaintenanceExpirationService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<QuotationMaintenanceExpirationService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 服務啟動時立即檢查一次，避免重啟後漏判資料。
        await CheckExpirationAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await CheckExpirationAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // 停止服務時避免丟出不必要的例外。
                break;
            }
        }
    }

    /// <summary>
    /// 執行估價單與維修單過期檢查的核心流程。
    /// </summary>
    private async Task CheckExpirationAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();

        var now = GetTaipeiNow();
        var expirationThreshold = now.AddDays(-60);

        // ---------- 資料查詢區 ----------
        // 估價單需同時檢查 110(估價中)、180(估價完成) 與 190(已預約) 的 60 天超時情況。
        var quotations = await context.Quatations
            .Where(q =>
                (q.Status != null && q.Status110Timestamp != null && q.Status == "110" && q.Status110Timestamp < expirationThreshold) ||
                (q.Status != null && q.Status180Timestamp != null && q.Status == "180" && q.Status180Timestamp < expirationThreshold) ||
                (q.Status != null && q.Status190Timestamp != null && q.Status == "190" && q.Status190Timestamp < expirationThreshold))
            .ToListAsync(cancellationToken);

        var maintenanceOrders = await context.Orders
            .Where(order =>
                order.Status != null &&
                order.Status220Date != null &&
                order.Status == "220" &&
                order.Status220Date < expirationThreshold)
            .ToListAsync(cancellationToken);

        // ---------- 方法區 ----------
        var expiredEstimationCount = 0;
        var expiredReservationCount = 0;
        foreach (var quotation in quotations)
        {
            if (string.Equals(quotation.Status, "110", StringComparison.OrdinalIgnoreCase))
            {
                ApplyExpiredQuotationStatus(quotation, "186", now);
                expiredEstimationCount++;
            }
            else if (string.Equals(quotation.Status, "180", StringComparison.OrdinalIgnoreCase))
            {
                // 估價完成超過 60 天仍未進一步處理，同樣需轉為 186 避免久置。
                ApplyExpiredQuotationStatus(quotation, "186", now);
                expiredEstimationCount++;
            }
            else if (string.Equals(quotation.Status, "190", StringComparison.OrdinalIgnoreCase))
            {
                ApplyExpiredQuotationStatus(quotation, "196", now);
                expiredReservationCount++;
            }
        }

        var expiredMaintenanceCount = 0;
        foreach (var order in maintenanceOrders)
        {
            ApplyExpiredMaintenanceStatus(order, now);
            expiredMaintenanceCount++;
        }

        if (expiredEstimationCount == 0 && expiredReservationCount == 0 && expiredMaintenanceCount == 0)
        {
            _logger.LogDebug("估價維修過期排程：目前無需更新的資料。");
            return;
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "估價維修過期排程完成：估價過期 {QuotationExpired} 筆、預約過期 {ReservationExpired} 筆、維修過期 {MaintenanceExpired} 筆。",
            expiredEstimationCount,
            expiredReservationCount,
            expiredMaintenanceCount);
    }

    /// <summary>
    /// 將估價單狀態更新為指定的過期代碼，並同步更新審核資訊。
    /// </summary>
    private static void ApplyExpiredQuotationStatus(Quatation quotation, string statusCode, DateTime timestamp)
    {
        quotation.Status = statusCode;
        quotation.ModificationTimestamp = timestamp;
        quotation.ModifiedBy = SchedulerOperator;
        // 依據需求僅更新狀態本身，避免同步寫入 CurrentStatus 相關欄位。
    }

    /// <summary>
    /// 將維修單狀態標記為 296（維修過期）。
    /// </summary>
    private static void ApplyExpiredMaintenanceStatus(Order order, DateTime timestamp)
    {
        order.Status = "296";
        order.ModificationTimestamp = timestamp;
        order.ModifiedBy = SchedulerOperator;
        // 維修單同樣只調整狀態，避免覆寫前台仍在使用的狀態時間與人員資訊。
    }

    /// <summary>
    /// 取得台北時區的現在時間，若系統缺少對應時區則以 UTC+8 補正。
    /// </summary>
    private static DateTime GetTaipeiNow()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var zoneId in TaipeiTimeZoneIds)
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                // 環境缺少該時區識別時改試下一個候選值。
            }
            catch (InvalidTimeZoneException)
            {
                // 當時區資訊異常時繼續嘗試下一組 ID。
            }
        }

        return utcNow.AddHours(8);
    }
}
