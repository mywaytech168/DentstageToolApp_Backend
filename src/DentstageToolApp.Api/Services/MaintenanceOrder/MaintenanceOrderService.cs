using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using DentstageToolApp.Api.MaintenanceOrders;
using DentstageToolApp.Api.Quotations;
using DentstageToolApp.Api.Services.Quotation;
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
    private const int SerialCandidateFetchCount = 50;

    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<MaintenanceOrderService> _logger;
    private readonly IQuotationService _quotationService;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public MaintenanceOrderService(
        DentstageToolAppContext dbContext,
        ILogger<MaintenanceOrderService> logger,
        IQuotationService quotationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _quotationService = quotationService;
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

    /// <inheritdoc />
    public async Task UpdateOrderAsync(UpdateMaintenanceOrderRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供維修單編輯資料。");
        }

        // ---------- 參數整理 ----------
        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");
        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        cancellationToken.ThrowIfCancellationRequested();

        // 取得維修單與估價單，後續需同步兩者內容。
        var order = await _dbContext.Orders
            .Include(entity => entity.Quatation)
            .FirstOrDefaultAsync(entity => entity.OrderNo == orderNo, cancellationToken);

        if (order is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無需更新的維修單。");
        }

        if (order.Quatation is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單缺少關聯估價單，無法同步更新。");
        }

        var quotationNo = order.Quatation.QuotationNo;
        if (string.IsNullOrWhiteSpace(quotationNo))
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "估價單編號缺失，無法同步維修資料。");
        }

        // ---------- 建立估價單更新請求 ----------
        var quotationUpdateRequest = new UpdateQuotationRequest
        {
            QuotationNo = quotationNo,
            Car = request.Car ?? new QuotationCarInfo(),
            Customer = request.Customer ?? new QuotationCustomerInfo(),
            CategoryRemarks = request.CategoryRemarks ?? new Dictionary<string, string?>(),
            Remark = request.Remark,
            Damages = request.Damages ?? new List<QuotationDamageItem>(),
            CarBodyConfirmation = request.CarBodyConfirmation,
            Maintenance = request.Maintenance
        };

        // 呼叫估價單服務沿用原邏輯，避免雙端處理流程不一致。
        await _quotationService.UpdateQuotationAsync(quotationUpdateRequest, operatorName, cancellationToken);

        // 重新載入估價單資訊，確保取得最新欄位。
        await _dbContext.Entry(order.Quatation).ReloadAsync(cancellationToken);

        var quotation = order.Quatation;
        var plainRemark = ExtractPlainRemark(quotation.Remark);
        var amount = CalculateOrderAmount(quotation.Valuation, quotation.Discount);

        // ---------- 同步維修單欄位 ----------
        order.CarUid = quotation.CarUid;
        order.CarNoInputGlobal = quotation.CarNoInputGlobal;
        order.CarNoInput = quotation.CarNoInput;
        order.CarNo = quotation.CarNo;
        order.Brand = quotation.Brand;
        order.Model = quotation.Model;
        order.Color = quotation.Color;
        order.CarRemark = quotation.CarRemark;
        order.BrandModel = quotation.BrandModel;
        order.CustomerUid = quotation.CustomerUid;
        order.CustomerType = quotation.CustomerType;
        order.PhoneInputGlobal = quotation.PhoneInputGlobal;
        order.PhoneInput = quotation.PhoneInput;
        order.Phone = quotation.Phone;
        order.Name = quotation.Name;
        order.Gender = quotation.Gender;
        order.Connect = quotation.Connect;
        order.County = quotation.County;
        order.Township = quotation.Township;
        order.Source = quotation.Source;
        order.Reason = quotation.Reason;
        order.Email = quotation.Email;
        order.ConnectRemark = quotation.ConnectRemark;
        order.BookDate = quotation.BookDate?.ToString("yyyy-MM-dd");
        order.WorkDate = quotation.FixDate?.ToString("yyyy-MM-dd");
        order.FixType = quotation.FixType;
        order.CarReserved = quotation.CarReserved;
        order.Content = plainRemark;
        order.Remark = quotation.Remark;
        order.Valuation = quotation.Valuation;
        order.DiscountPercent = quotation.DiscountPercent;
        order.Discount = quotation.Discount;
        order.DiscountReason = quotation.DiscountReason;
        order.Amount = amount;
        order.ModificationTimestamp = now;
        order.ModifiedBy = operatorLabel;
        order.CurrentStatusDate = now;
        order.CurrentStatusUser = operatorLabel;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 更新維修單 {OrderNo} 完成。", operatorLabel, order.OrderNo);
    }

    /// <inheritdoc />
    public async Task<MaintenanceOrderContinuationResponse> ContinueOrderAsync(MaintenanceOrderContinueRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供續修維修單的條件。");
        }

        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");
        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        cancellationToken.ThrowIfCancellationRequested();

        var sourceOrder = await _dbContext.Orders
            .Include(entity => entity.Quatation)
            .FirstOrDefaultAsync(entity => entity.OrderNo == orderNo, cancellationToken);

        if (sourceOrder is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無需續修的維修單。");
        }

        if (sourceOrder.Quatation is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單缺少估價資料，無法續修。");
        }

        var quotation = sourceOrder.Quatation;
        var orderSerial = await GenerateNextOrderSerialAsync(now, cancellationToken);
        var orderUid = BuildOrderUid();
        var orderNoNew = BuildOrderNo(orderSerial, now);
        var plainRemark = ExtractPlainRemark(quotation.Remark);
        var amount = CalculateOrderAmount(quotation.Valuation, quotation.Discount);

        // ---------- 建立新的維修單實體 ----------
        var newOrder = new Order
        {
            OrderUid = orderUid,
            OrderNo = orderNoNew,
            SerialNum = orderSerial,
            CreationTimestamp = now,
            CreatedBy = operatorLabel,
            ModificationTimestamp = now,
            ModifiedBy = operatorLabel,
            UserUid = NormalizeOptionalText(quotation.UserUid) ?? quotation.TechnicianUid ?? operatorLabel,
            UserName = NormalizeOptionalText(quotation.UserName) ?? operatorLabel,
            StoreUid = quotation.StoreUid ?? sourceOrder.StoreUid,
            Date = DateOnly.FromDateTime(now),
            Status = "210",
            Status210Date = now,
            Status210User = operatorLabel,
            CurrentStatusDate = now,
            CurrentStatusUser = operatorLabel,
            QuatationUid = quotation.QuotationUid,
            CarUid = quotation.CarUid,
            CarNoInputGlobal = quotation.CarNoInputGlobal,
            CarNoInput = quotation.CarNoInput,
            CarNo = quotation.CarNo,
            Brand = quotation.Brand,
            Model = quotation.Model,
            Color = quotation.Color,
            CarRemark = quotation.CarRemark,
            BrandModel = quotation.BrandModel,
            CustomerUid = quotation.CustomerUid,
            CustomerType = quotation.CustomerType,
            PhoneInputGlobal = quotation.PhoneInputGlobal,
            PhoneInput = quotation.PhoneInput,
            Phone = quotation.Phone,
            Name = quotation.Name,
            Gender = quotation.Gender,
            Connect = quotation.Connect,
            County = quotation.County,
            Township = quotation.Township,
            Source = quotation.Source,
            Reason = quotation.Reason,
            Email = quotation.Email,
            ConnectRemark = quotation.ConnectRemark,
            BookDate = quotation.BookDate?.ToString("yyyy-MM-dd"),
            BookMethod = quotation.BookMethod,
            WorkDate = quotation.FixDate?.ToString("yyyy-MM-dd"),
            FixType = quotation.FixType,
            CarReserved = quotation.CarReserved,
            Content = plainRemark,
            Remark = quotation.Remark,
            Valuation = quotation.Valuation,
            DiscountPercent = quotation.DiscountPercent,
            Discount = quotation.Discount,
            DiscountReason = quotation.DiscountReason,
            Amount = amount,
            FlagRegularCustomer = quotation.FlagRegularCustomer,
            FlagExternalCooperation = sourceOrder.FlagExternalCooperation
        };

        await _dbContext.Orders.AddAsync(newOrder, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 針對維修單 {SourceOrder} 建立續修單 {NewOrder}。",
            operatorLabel,
            sourceOrder.OrderNo,
            newOrder.OrderNo);

        return new MaintenanceOrderContinuationResponse
        {
            OrderUid = newOrder.OrderUid,
            OrderNo = newOrder.OrderNo ?? string.Empty,
            QuotationUid = quotation.QuotationUid,
            QuotationNo = quotation.QuotationNo,
            CreatedAt = newOrder.CreationTimestamp ?? now,
            Status = newOrder.Status ?? "210",
            Message = "已建立新的續修維修單。"
        };
    }

    /// <inheritdoc />
    public async Task<MaintenanceOrderStatusChangeResponse> CompleteOrderAsync(MaintenanceOrderCompleteRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供維修完成的條件。");
        }

        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");
        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        cancellationToken.ThrowIfCancellationRequested();

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.OrderNo == orderNo, cancellationToken);

        if (order is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無需標記完成的維修單。");
        }

        if (string.Equals(order.Status, "295", StringComparison.OrdinalIgnoreCase))
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單已終止，無法標記為完成。");
        }

        if (string.Equals(order.Status, "290", StringComparison.OrdinalIgnoreCase))
        {
            return new MaintenanceOrderStatusChangeResponse
            {
                OrderUid = order.OrderUid,
                OrderNo = order.OrderNo,
                Status = order.Status,
                StatusTime = order.Status290Date,
                Message = "維修單已處於完成狀態。"
            };
        }

        order.Status = "290";
        order.Status290Date = now;
        order.Status290User = operatorLabel;
        order.CurrentStatusDate = now;
        order.CurrentStatusUser = operatorLabel;
        order.ModificationTimestamp = now;
        order.ModifiedBy = operatorLabel;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將維修單 {OrderNo} 標記為完成。", operatorLabel, order.OrderNo);

        return new MaintenanceOrderStatusChangeResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            Status = order.Status,
            StatusTime = order.Status290Date,
            Message = "維修單已更新為維修完成。"
        };
    }

    /// <inheritdoc />
    public async Task<MaintenanceOrderStatusChangeResponse> TerminateOrderAsync(MaintenanceOrderTerminateRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.BadRequest, "請提供終止維修的條件。");
        }

        var orderNo = NormalizeRequiredText(request.OrderNo, "維修單編號");
        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        cancellationToken.ThrowIfCancellationRequested();

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(entity => entity.OrderNo == orderNo, cancellationToken);

        if (order is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無需終止的維修單。");
        }

        if (string.Equals(order.Status, "295", StringComparison.OrdinalIgnoreCase))
        {
            return new MaintenanceOrderStatusChangeResponse
            {
                OrderUid = order.OrderUid,
                OrderNo = order.OrderNo,
                Status = order.Status,
                StatusTime = order.Status295Timestamp,
                Message = "維修單已處於終止狀態。"
            };
        }

        order.Status = "295";
        order.Status295Timestamp = now;
        order.Status295User = operatorLabel;
        order.CurrentStatusDate = now;
        order.CurrentStatusUser = operatorLabel;
        order.ModificationTimestamp = now;
        order.ModifiedBy = operatorLabel;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將維修單 {OrderNo} 標記為終止。", operatorLabel, order.OrderNo);

        return new MaintenanceOrderStatusChangeResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            Status = order.Status,
            StatusTime = order.Status295Timestamp,
            Message = "維修單已更新為終止。"
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
    /// 解析 remark，若為 JSON 結構則取出 PlainRemark，否則回傳原始內容。
    /// </summary>
    private static string? ExtractPlainRemark(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(remark);
            if (document.RootElement.TryGetProperty("plainRemark", out var plainElement) && plainElement.ValueKind == JsonValueKind.String)
            {
                return NormalizeOptionalText(plainElement.GetString());
            }
        }
        catch (JsonException)
        {
            // remark 為純文字格式時會進入此區塊，直接回傳原字串即可。
        }

        return remark;
    }

    /// <summary>
    /// 計算維修單應收金額，避免折扣造成負數。
    /// </summary>
    private static decimal? CalculateOrderAmount(decimal? valuation, decimal? discount)
    {
        if (!valuation.HasValue)
        {
            return null;
        }

        var amount = valuation.Value - (discount ?? 0m);
        return amount < 0 ? 0m : amount;
    }

    /// <summary>
    /// 建立維修單唯一識別碼，統一採用 O_ 前綴。
    /// </summary>
    private static string BuildOrderUid()
    {
        return $"O_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// 依日期與序號產生維修單編號。
    /// </summary>
    private static string BuildOrderNo(int serial, DateTime timestamp)
    {
        return $"O{timestamp:yyMM}{serial:D4}";
    }

    /// <summary>
    /// 產生下一個維修單序號，沿用每月遞增規則。
    /// </summary>
    private async Task<int> GenerateNextOrderSerialAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        var prefix = $"O{timestamp:yyMM}";

        var prefixCandidates = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => !string.IsNullOrEmpty(order.OrderNo) && EF.Functions.Like(order.OrderNo!, prefix + "%"))
            .OrderByDescending(order => order.SerialNum)
            .ThenByDescending(order => order.OrderNo)
            .Select(order => new SerialCandidate(order.SerialNum, order.OrderNo))
            .Take(SerialCandidateFetchCount)
            .ToListAsync(cancellationToken);

        var maxSerial = ExtractMaxSerial(prefixCandidates, prefix);

        if (maxSerial == 0)
        {
            var monthStart = new DateTime(timestamp.Year, timestamp.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthCandidates = await _dbContext.Orders
                .AsNoTracking()
                .Where(order => order.CreationTimestamp >= monthStart && order.CreationTimestamp < monthEnd)
                .OrderByDescending(order => order.SerialNum)
                .ThenByDescending(order => order.OrderNo)
                .Select(order => new SerialCandidate(order.SerialNum, order.OrderNo))
                .Take(SerialCandidateFetchCount)
                .ToListAsync(cancellationToken);

            maxSerial = ExtractMaxSerial(monthCandidates, prefix);
        }

        return maxSerial + 1;
    }

    /// <summary>
    /// 從候選清單取出最大序號，必要時解析舊資料的編號。
    /// </summary>
    private static int ExtractMaxSerial(IEnumerable<SerialCandidate> candidates, string prefix)
    {
        var maxSerial = 0;

        foreach (var candidate in candidates)
        {
            if (candidate.SerialNum is int serial && serial > maxSerial)
            {
                maxSerial = serial;
            }

            if (candidate.DocumentNo is string documentNo)
            {
                var parsedSerial = TryParseSerialFromDocumentNo(documentNo, prefix);
                if (parsedSerial.HasValue && parsedSerial.Value > maxSerial)
                {
                    maxSerial = parsedSerial.Value;
                }
            }
        }

        return maxSerial;
    }

    /// <summary>
    /// 嘗試由維修單編號解析序號，無法解析時回傳 null。
    /// </summary>
    private static int? TryParseSerialFromDocumentNo(string documentNo, string prefix)
    {
        if (string.IsNullOrWhiteSpace(documentNo) || !documentNo.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var serialText = documentNo[prefix.Length..];
        return int.TryParse(serialText, out var result) ? result : null;
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

    /// <summary>
    /// 維修單序號候選資料結構，封裝資料表上的序號與編號欄位。
    /// </summary>
    private sealed record SerialCandidate(int? SerialNum, string? DocumentNo);
}
