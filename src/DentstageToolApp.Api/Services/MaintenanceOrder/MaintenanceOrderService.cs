using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.MaintenanceOrders;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.MaintenanceOrder;

/// <summary>
/// 維修單服務實作，負責處理列表查詢、詳細資料與狀態異動邏輯。
/// </summary>
public class MaintenanceOrderService : IMaintenanceOrderService
{
    private static readonly string[] TaipeiTimeZoneIds = { "Taipei Standard Time", "Asia/Taipei" };

    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<MaintenanceOrderService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public MaintenanceOrderService(DentstageToolAppContext dbContext, ILogger<MaintenanceOrderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <inheritdoc />
    public async Task<MaintenanceOrderListResponse> GetOrdersAsync(MaintenanceOrderListQuery query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query ?? new MaintenanceOrderListQuery();
        var page = Math.Max(1, normalizedQuery.Page);
        var pageSize = Math.Clamp(normalizedQuery.PageSize, 1, 200);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 查詢條件組裝 ----------
        var ordersQuery = _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Quatation)
                .ThenInclude(quotation => quotation.TechnicianNavigation)
            .Include(order => order.Quatation)
                .ThenInclude(quotation => quotation.StoreNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery.FixType))
        {
            var fixType = normalizedQuery.FixType.Trim();
            ordersQuery = ordersQuery.Where(order => order.FixType == fixType);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.Status) && !string.Equals(normalizedQuery.Status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var status = normalizedQuery.Status.Trim();
            ordersQuery = ordersQuery.Where(order => order.Status == status);
        }

        if (normalizedQuery.StartDate.HasValue)
        {
            var start = normalizedQuery.StartDate.Value.Date;
            ordersQuery = ordersQuery.Where(order => order.CreationTimestamp >= start);
        }

        if (normalizedQuery.EndDate.HasValue)
        {
            var endExclusive = normalizedQuery.EndDate.Value.Date.AddDays(1);
            ordersQuery = ordersQuery.Where(order => order.CreationTimestamp < endExclusive);
        }

        // ---------- 分頁與排序 ----------
        var totalCount = await ordersQuery.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;

        var orderEntities = await ordersQuery
            .OrderByDescending(order => order.CreationTimestamp ?? DateTime.MinValue)
            .ThenByDescending(order => order.OrderNo)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // ---------- 組裝列表 ----------
        var items = orderEntities
            .Select(MapToSummary)
            .ToList();

        return new MaintenanceOrderListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<MaintenanceOrderDetailResponse> GetOrderAsync(MaintenanceOrderDetailRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供查詢條件。");
        }

        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");

        cancellationToken.ThrowIfCancellationRequested();

        var orderEntity = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Quatation)
                .ThenInclude(quotation => quotation.TechnicianNavigation)
            .Include(order => order.Quatation)
                .ThenInclude(quotation => quotation.StoreNavigation)
            .FirstOrDefaultAsync(order => order.OrderNo == orderNo, cancellationToken);

        if (orderEntity is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無符合條件的維修單。");
        }

        return MapToDetail(orderEntity);
    }

    /// <inheritdoc />
    public async Task<MaintenanceOrderStatusChangeResponse> RevertOrderAsync(MaintenanceOrderRevertRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供回溯條件。");
        }

        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");
        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        cancellationToken.ThrowIfCancellationRequested();

        var order = await _dbContext.Orders
            .Include(entity => entity.Quatation)
                .ThenInclude(quotation => quotation.TechnicianNavigation)
            .Include(entity => entity.Quatation)
                .ThenInclude(quotation => quotation.StoreNavigation)
            .FirstOrDefaultAsync(entity => entity.OrderNo == orderNo, cancellationToken);

        if (order is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無需回溯的維修單。");
        }

        if (order.Quatation is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單缺少關聯估價單，無法回溯。");
        }

        if (string.Equals(order.Status, "295", StringComparison.OrdinalIgnoreCase))
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單已標記為取消，無需再次回溯。");
        }

        // ---------- 回溯估價單狀態 ----------
        var previousStatus = ResolvePreviousStatus(order.Quatation);
        if (previousStatus is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "估價單缺少可回溯的上一個狀態。");
        }

        if (IsCancellationStatus(order.Quatation.Status))
        {
            ClearCancellationAudit(order.Quatation);
        }

        ApplyQuotationStatus(order.Quatation, previousStatus, operatorLabel, now);

        // ---------- 更新維修單狀態為取消 ----------
        order.Status = "295";
        order.Status295Timestamp = now;
        order.Status295User = operatorLabel;
        order.CurrentStatusDate = now;
        order.CurrentStatusUser = operatorLabel;
        order.ModificationTimestamp = now;
        order.ModifiedBy = operatorLabel;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 將維修單 {OrderNo} 回溯至估價狀態 {Status}，並將維修單標記為取消。",
            operatorLabel,
            order.OrderNo,
            previousStatus);

        return new MaintenanceOrderStatusChangeResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            Status = order.Status,
            StatusTime = order.Status295Timestamp,
            Message = "已回溯至估價階段並取消維修單。"
        };
    }

    /// <inheritdoc />
    public async Task<MaintenanceOrderStatusChangeResponse> ConfirmMaintenanceAsync(MaintenanceOrderConfirmRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供確認維修的條件。");
        }

        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");
        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        cancellationToken.ThrowIfCancellationRequested();

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.OrderNo == orderNo, cancellationToken);

        if (order is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無需確認的維修單。");
        }

        if (string.Equals(order.Status, "295", StringComparison.OrdinalIgnoreCase))
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單已取消，無法確認維修。");
        }

        if (string.Equals(order.Status, "220", StringComparison.OrdinalIgnoreCase))
        {
            return new MaintenanceOrderStatusChangeResponse
            {
                OrderUid = order.OrderUid,
                OrderNo = order.OrderNo,
                Status = order.Status,
                StatusTime = order.Status220Date,
                Message = "維修單已處於維修中狀態。"
            };
        }

        order.Status = "220";
        order.Status220Date = now;
        order.Status220User = operatorLabel;
        order.CurrentStatusDate = now;
        order.CurrentStatusUser = operatorLabel;
        order.ModificationTimestamp = now;
        order.ModifiedBy = operatorLabel;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將維修單 {OrderNo} 標記為維修中。", operatorLabel, order.OrderNo);

        return new MaintenanceOrderStatusChangeResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            Status = order.Status,
            StatusTime = order.Status220Date,
            Message = "維修單已更新為維修中。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將維修單實體轉為列表摘要資料。
    /// </summary>
    private static MaintenanceOrderSummaryResponse MapToSummary(Order order)
    {
        var quotation = order.Quatation;
        var storeName = quotation?.StoreNavigation?.StoreName;
        var estimatorName = quotation?.TechnicianNavigation?.TechnicianName
            ?? NormalizeOptionalText(quotation?.UserName)
            ?? quotation?.CurrentStatusUser;

        return new MaintenanceOrderSummaryResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            Status = order.Status,
            CustomerName = order.Name,
            Phone = order.Phone,
            CarBrand = order.Brand,
            CarModel = order.Model,
            CarPlate = order.CarNo,
            StoreName = NormalizeOptionalText(storeName) ?? NormalizeOptionalText(order.StoreUid),
            EstimatorName = NormalizeOptionalText(estimatorName),
            CreatorName = NormalizeOptionalText(order.UserName),
            CreatedAt = order.CreationTimestamp
        };
    }

    /// <summary>
    /// 將維修單實體轉為詳細資料回應。
    /// </summary>
    private static MaintenanceOrderDetailResponse MapToDetail(Order order)
    {
        var quotation = order.Quatation;
        var storeName = quotation?.StoreNavigation?.StoreName;
        var estimatorName = quotation?.TechnicianNavigation?.TechnicianName
            ?? NormalizeOptionalText(quotation?.UserName)
            ?? quotation?.CurrentStatusUser;

        return new MaintenanceOrderDetailResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            QuotationUid = quotation?.QuotationUid,
            QuotationNo = quotation?.QuotationNo,
            Status = order.Status,
            FixType = order.FixType,
            CreatedAt = order.CreationTimestamp,
            UpdatedAt = order.ModificationTimestamp,
            CreatorName = NormalizeOptionalText(order.UserName),
            StoreName = NormalizeOptionalText(storeName) ?? NormalizeOptionalText(order.StoreUid),
            EstimatorName = NormalizeOptionalText(estimatorName),
            CustomerName = order.Name,
            CustomerPhone = order.Phone,
            CarPlate = order.CarNo,
            CarBrand = order.Brand,
            CarModel = order.Model,
            CarColor = order.Color,
            BookDate = order.BookDate,
            WorkDate = order.WorkDate,
            Remark = order.Remark,
            Valuation = order.Valuation,
            Discount = order.Discount,
            Amount = order.Amount,
            Status210Date = order.Status210Date,
            Status220Date = order.Status220Date,
            Status290Date = order.Status290Date,
            Status295Date = order.Status295Timestamp,
            CurrentStatusUser = order.CurrentStatusUser
        };
    }

    /// <summary>
    /// 將估價單狀態更新為指定代碼並記錄審核資訊。
    /// </summary>
    private static void ApplyQuotationStatus(Quatation quotation, string statusCode, string operatorLabel, DateTime timestamp)
    {
        quotation.Status = statusCode;
        quotation.ModificationTimestamp = timestamp;
        quotation.ModifiedBy = operatorLabel;
        quotation.CurrentStatusDate = timestamp;
        quotation.CurrentStatusUser = operatorLabel;

        switch (statusCode)
        {
            case "110":
                quotation.Status110Timestamp = timestamp;
                quotation.Status110User = operatorLabel;
                break;
            case "180":
                quotation.Status180Timestamp = timestamp;
                quotation.Status180User = operatorLabel;
                break;
            case "190":
                quotation.Status190Timestamp = timestamp;
                quotation.Status190User = operatorLabel;
                break;
            case "191":
                quotation.Status191Timestamp = timestamp;
                quotation.Status191User = operatorLabel;
                break;
            case "195":
                quotation.Status199Timestamp = timestamp;
                quotation.Status199User = operatorLabel;
                break;
        }
    }

    /// <summary>
    /// 由歷史狀態時間判斷上一個狀態碼。
    /// </summary>
    private static string? ResolvePreviousStatus(Quatation quotation)
    {
        var currentStatus = NormalizeOptionalText(quotation.Status);
        var history = new List<(string Code, DateTime? Timestamp)>
        {
            ("195", quotation.Status199Timestamp),
            ("191", quotation.Status191Timestamp),
            ("190", quotation.Status190Timestamp),
            ("180", quotation.Status180Timestamp),
            ("110", quotation.Status110Timestamp)
        };

        var ordered = history
            .Where(item => item.Timestamp.HasValue)
            .OrderByDescending(item => item.Timestamp!.Value)
            .ToList();

        foreach (var (code, _) in ordered)
        {
            if (!string.Equals(code, currentStatus, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return null;
    }

    /// <summary>
    /// 判斷是否屬於取消狀態。
    /// </summary>
    private static bool IsCancellationStatus(string? status)
    {
        var normalized = NormalizeOptionalText(status);
        return string.Equals(normalized, "195", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 回溯取消狀態時清除取消紀錄欄位。
    /// </summary>
    private static void ClearCancellationAudit(Quatation quotation)
    {
        quotation.Reject = false;
        quotation.RejectReason = null;
        quotation.Status199Timestamp = null;
        quotation.Status199User = null;
    }

    /// <summary>
    /// 正規化必填文字欄位。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, $"請提供{fieldName}。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 正規化可選文字欄位，若為空則回傳 null。
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// 正規化操作人名稱，避免寫入空白字串。
    /// </summary>
    private static string NormalizeOperator(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return "UnknownUser";
        }

        return operatorName.Trim();
    }

    /// <summary>
    /// 取得台北當地時間，若系統缺少時區設定則以 UTC+8 回傳。
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
                // 繼續嘗試下一個時區識別碼。
            }
            catch (InvalidTimeZoneException)
            {
                // 若時區設定異常，同樣嘗試下一個識別碼。
            }
        }

        return utcNow.AddHours(8);
    }
}
