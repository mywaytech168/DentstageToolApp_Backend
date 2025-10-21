using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using DentstageToolApp.Api.Models.Quotations;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Services.Quotation;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly PhotoStorageOptions _photoStorageOptions;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public MaintenanceOrderService(
        DentstageToolAppContext dbContext,
        ILogger<MaintenanceOrderService> logger,
        IQuotationService quotationService,
        IOptions<PhotoStorageOptions> photoStorageOptions)
    {
        _dbContext = dbContext;
        _logger = logger;
        _quotationService = quotationService;
        _photoStorageOptions = photoStorageOptions?.Value ?? new PhotoStorageOptions();
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
                .ThenInclude(quotation => quotation.EstimationTechnicianNavigation)
            .Include(order => order.Quatation)
                .ThenInclude(quotation => quotation.StoreNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery.FixType))
        {
            var fixType = normalizedQuery.FixType.Trim();
            var normalizedFilter = QuotationDamageFixTypeHelper.Normalize(fixType);

            if (normalizedFilter is null)
            {
                var resolved = QuotationDamageFixTypeHelper.ResolveDisplayName(fixType);
                if (string.Equals(resolved, fixType, StringComparison.Ordinal))
                {
                    ordersQuery = ordersQuery.Where(order => order.FixType == resolved);
                }
            }
            else
            {
                ordersQuery = ordersQuery.Where(order => order.FixType == normalizedFilter);
            }
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
                .ThenInclude(quotation => quotation.EstimationTechnicianNavigation)
            .Include(order => order.Quatation)
                .ThenInclude(quotation => quotation.StoreNavigation)
            .FirstOrDefaultAsync(order => order.OrderNo == orderNo, cancellationToken);

        if (orderEntity is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.NotFound, "查無符合條件的維修單。");
        }

        // ---------- 同步估價單詳情 ----------
        // 先嘗試取得估價單詳情，成功時可沿用估價單的完整巢狀結構，失敗時則回退至維修單原始欄位。
        var quotationDetail = await TryGetQuotationDetailAsync(orderEntity, cancellationToken);

        return MapToDetail(orderEntity, quotationDetail);
    }

    /// <summary>
    /// 嘗試以維修單關聯的估價單編號取得估價詳情，若失敗則記錄警告並回傳 null。
    /// </summary>
    private async Task<QuotationDetailResponse?> TryGetQuotationDetailAsync(Order order, CancellationToken cancellationToken)
    {
        if (order is null)
        {
            return null;
        }

        var quotationNo = NormalizeOptionalText(order.Quatation?.QuotationNo);
        if (quotationNo is null)
        {
            return null;
        }

        try
        {
            var request = new GetQuotationRequest { QuotationNo = quotationNo };
            return await _quotationService.GetQuotationAsync(request, cancellationToken);
        }
        catch (QuotationManagementException ex)
        {
            // 若估價單已被移除或資料不完整，保留警告紀錄並沿用維修單既有資料。
            _logger.LogWarning(
                ex,
                "維修單 {OrderNo} 取得估價單詳情失敗：{Message}",
                order.OrderNo,
                ex.Message);
        }
        catch (Exception ex)
        {
            // 非預期錯誤同樣記錄，避免影響後續維修單資料輸出。
            _logger.LogError(
                ex,
                "維修單 {OrderNo} 取得估價單詳情時發生未預期錯誤。",
                order.OrderNo);
        }

        return null;
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
                .ThenInclude(quotation => quotation.EstimationTechnicianNavigation)
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

        // ---------- 決定可回復的上一個維修狀態 ----------
        var currentStatus = NormalizeOptionalText(order.Status);
        if (currentStatus is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單缺少狀態資訊，無法回溯。");
        }

        var previousOrderStatus = ResolvePreviousOrderStatus(order, currentStatus);
        if (previousOrderStatus is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單已位於最早狀態，無法再回溯。");
        }

        // ---------- 回溯估價單狀態 ----------
        var previousQuotationStatus = ResolvePreviousQuotationStatus(order.Quatation);
        if (previousQuotationStatus is null)
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "估價單缺少可回溯的上一個狀態。");
        }

        if (IsCancellationStatus(order.Quatation.Status))
        {
            ClearCancellationAudit(order.Quatation);
        }

        ApplyQuotationStatus(order.Quatation, previousQuotationStatus, operatorLabel, now);

        // ---------- 更新維修單狀態 ----------
        ApplyOrderStatusReversion(order, previousOrderStatus, operatorLabel, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var statusTimestamp = GetOrderStatusTimestamp(order, previousOrderStatus) ?? order.CurrentStatusDate;

        _logger.LogInformation(
            "操作人員 {Operator} 將維修單 {OrderNo} 回溯至狀態 {Status}，並同步回復估價單狀態 {QuotationStatus}。",
            operatorLabel,
            order.OrderNo,
            previousOrderStatus,
            previousQuotationStatus);

        return new MaintenanceOrderStatusChangeResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            Status = previousOrderStatus,
            StatusTime = statusTimestamp,
            Message = $"維修單已回復至狀態 {previousOrderStatus}。"
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

        var requestQuotationNo = NormalizeOptionalText(request.QuotationNo);
        if (requestQuotationNo is not null && !string.Equals(requestQuotationNo, quotationNo, StringComparison.OrdinalIgnoreCase))
        {
            throw new MaintenanceOrderManagementException(HttpStatusCode.Conflict, "維修單與估價單編號不一致，請確認送出的資料。");
        }

        // 將估價單編號同步寫回請求物件，沿用估價編輯結構以利 Swagger 呈現一致欄位。
        request.QuotationNo = quotationNo;

        // 呼叫估價單服務沿用原邏輯，避免雙端處理流程不一致。
        await _quotationService.UpdateQuotationAsync(request, operatorName, cancellationToken);

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
        order.Milage = quotation.Milage;
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
        order.BookMethod = quotation.BookMethod;
        order.WorkDate = quotation.FixDate?.ToString("yyyy-MM-dd");
        order.FixType = quotation.FixType;
        order.CarReserved = quotation.CarReserved;
        order.Content = plainRemark;
        order.Remark = quotation.Remark;
        order.Valuation = quotation.Valuation;
        order.DiscountPercent = quotation.DiscountPercent;
        order.Discount = quotation.Discount;
        order.DiscountReason = quotation.DiscountReason;
        // ---------- 儲存估價技師 UID，讓維修單查詢不必再回頭查估價單 ----------
        order.EstimationTechnicianUid = NormalizeOptionalText(quotation.EstimationTechnicianUid)
            ?? NormalizeOptionalText(quotation.UserUid);
        order.CreatorTechnicianUid = quotation.CreatorTechnicianUid;
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
        var quotationSerial = await GenerateNextQuotationSerialAsync(now, cancellationToken);
        var quotationUidNew = BuildQuotationUid();
        var quotationNoNew = BuildQuotationNo(quotationSerial, now);
        var newQuotation = CloneQuotationForContinuation(
            quotation,
            quotationUidNew,
            quotationNoNew,
            quotationSerial,
            operatorLabel,
            now);

        // ---------- 原維修單狀態更新 ----------
        sourceOrder.Status = "295";
        sourceOrder.Status295Timestamp = now;
        sourceOrder.Status295User = operatorLabel;
        sourceOrder.CurrentStatusDate = now;
        sourceOrder.CurrentStatusUser = operatorLabel;
        sourceOrder.ModificationTimestamp = now;
        sourceOrder.ModifiedBy = operatorLabel;
        sourceOrder.StopReason = "續修取消維修";

        await _dbContext.Quatations.AddAsync(newQuotation, cancellationToken);

        // ---------- 圖片複製 ----------
        var photoUidMap = await DuplicatePhotosForContinuationAsync(
            quotation?.QuotationUid,
            newQuotation.QuotationUid,
            cancellationToken);

        var updatedRemark = ReplacePhotoUids(newQuotation.Remark, photoUidMap);
        newQuotation.Remark = updatedRemark;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 針對維修單 {SourceOrder} 複製估價單 {NewQuotation} 並取消原單。",
            operatorLabel,
            sourceOrder.OrderNo,
            newQuotation.QuotationNo);

        return new MaintenanceOrderContinuationResponse
        {
            CancelledOrderUid = sourceOrder.OrderUid ?? string.Empty,
            CancelledOrderNo = sourceOrder.OrderNo ?? string.Empty,
            QuotationUid = newQuotation.QuotationUid,
            QuotationNo = newQuotation.QuotationNo,
            CreatedAt = newQuotation.CreationTimestamp ?? now,
            Message = "已複製估價與圖片，原維修單已標記為取消維修。"
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
        // ---------- 估價技師名稱以維修單優先，其次才回退估價單資料 ----------
        var estimationTechnicianName = quotation?.EstimationTechnicianNavigation?.TechnicianName
            ?? NormalizeOptionalText(quotation?.UserName)
            ?? NormalizeOptionalText(order.UserName)
            ?? quotation?.CurrentStatusUser;
        // ---------- UID 先讀取維修單欄位，避免每次查詢都回頭 Join 估價單 ----------
        var estimationTechnicianUid = NormalizeOptionalText(order.EstimationTechnicianUid)
            ?? NormalizeOptionalText(order.UserUid)
            ?? NormalizeOptionalText(quotation?.EstimationTechnicianUid)
            ?? NormalizeOptionalText(quotation?.UserUid);
        var creatorUid = NormalizeOptionalText(order.CreatorTechnicianUid)
            ?? NormalizeOptionalText(quotation?.CreatorTechnicianUid);

        var creatorName = NormalizeOptionalText(quotation?.CreatedBy)
            ?? NormalizeOptionalText(order.UserName);

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
            EstimationTechnicianUid = estimationTechnicianUid,
            CreatorTechnicianUid = creatorUid,
            StoreName = NormalizeOptionalText(storeName) ?? NormalizeOptionalText(order.StoreUid),
            EstimationTechnicianName = NormalizeOptionalText(estimationTechnicianName),
            CreatorTechnicianName = creatorName,
            CreatedAt = order.CreationTimestamp
        };
    }

    /// <summary>
    /// 將維修單實體與估價詳情整併成統一輸出格式。
    /// </summary>
    private static MaintenanceOrderDetailResponse MapToDetail(Order order, QuotationDetailResponse? quotationDetail)
    {
        var quotation = order.Quatation;
        var storeInfo = BuildStoreInfo(order, quotationDetail?.Store);
        var carInfo = BuildCarInfo(order, quotationDetail?.Car);
        var customerInfo = BuildCustomerInfo(order, quotationDetail?.Customer);
        var damages = CloneDamageSummaries(quotationDetail?.Damages);
        var carBody = CloneCarBodyConfirmation(quotationDetail?.CarBodyConfirmation);
        var maintenance = BuildMaintenanceDetail(order, quotationDetail?.Maintenance);
        var amountInfo = BuildAmountInfo(order);
        var statusHistory = BuildStatusHistory(order);

        return new MaintenanceOrderDetailResponse
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            QuotationUid = quotationDetail?.QuotationUid ?? quotation?.QuotationUid,
            QuotationNo = quotationDetail?.QuotationNo ?? quotation?.QuotationNo,
            Status = NormalizeOptionalText(order.Status) ?? quotationDetail?.Status,
            CreatedAt = order.CreationTimestamp ?? quotationDetail?.CreatedAt,
            UpdatedAt = order.ModificationTimestamp ?? quotationDetail?.UpdatedAt ?? order.CreationTimestamp,
            Store = storeInfo,
            Car = carInfo,
            Customer = customerInfo,
            Damages = damages,
            CarBodyConfirmation = carBody,
            Maintenance = maintenance,
            Amounts = amountInfo,
            StatusHistory = statusHistory,
            CurrentStatusUser = statusHistory.CurrentStatusUser
        };
    }

    /// <summary>
    /// 組裝店鋪資訊，優先使用維修單最新資料，若缺少則回退估價單內容。
    /// </summary>
    private static QuotationStoreInfo BuildStoreInfo(Order order, QuotationStoreInfo? quotationStore)
    {
        var quotation = order.Quatation;
        var normalizedStoreName = NormalizeOptionalText(quotation?.StoreNavigation?.StoreName)
            ?? NormalizeOptionalText(order.StoreUid)
            ?? quotationStore?.StoreName;

        var reservationDate = ParseOptionalDate(order.BookDate)
            ?? quotation?.BookDate?.ToDateTime(TimeOnly.MinValue)
            ?? quotationStore?.ReservationDate;

        var repairDate = ParseOptionalDate(order.WorkDate)
            ?? quotation?.FixDate?.ToDateTime(TimeOnly.MinValue)
            ?? quotationStore?.RepairDate;

        // ---------- 優先使用維修單寫入的估價技師 UID，再回退至估價單或原始請求 ----------
        var estimationTechnicianUid = NormalizeOptionalText(order.EstimationTechnicianUid)
            ?? NormalizeOptionalText(order.UserUid)
            ?? NormalizeOptionalText(quotation?.EstimationTechnicianUid)
            ?? NormalizeOptionalText(quotation?.UserUid)
            ?? quotationStore?.EstimationTechnicianUid
            ?? quotationStore?.UserUid;
        var creatorUid = NormalizeOptionalText(order.CreatorTechnicianUid)
            ?? NormalizeOptionalText(quotation?.CreatorTechnicianUid)
            ?? quotationStore?.CreatorTechnicianUid
            ?? quotationStore?.UserUid;

        var resolvedEstimationTechnicianUid = NormalizeOptionalText(quotation?.EstimationTechnicianUid)
            ?? estimationTechnicianUid;
        var resolvedCreatorTechnicianUid = NormalizeOptionalText(quotation?.CreatorTechnicianUid)
            ?? creatorUid;
        var resolvedEstimationTechnicianName = NormalizeOptionalText(quotation?.EstimationTechnicianNavigation?.TechnicianName)
            ?? NormalizeOptionalText(quotation?.UserName)
            ?? NormalizeOptionalText(order.UserName)
            ?? quotationStore?.EstimationTechnicianName;
        var resolvedCreatorName = NormalizeOptionalText(quotation?.CreatedBy)
            ?? NormalizeOptionalText(order.UserName)
            ?? quotationStore?.CreatorTechnicianName;

        return new QuotationStoreInfo
        {
            StoreUid = NormalizeOptionalText(order.StoreUid)
                ?? NormalizeOptionalText(quotation?.StoreUid)
                ?? quotationStore?.StoreUid,
            UserUid = resolvedEstimationTechnicianUid,
            EstimationTechnicianUid = resolvedEstimationTechnicianUid,
            CreatorTechnicianUid = resolvedCreatorTechnicianUid,
            StoreName = normalizedStoreName,
            EstimationTechnicianName = resolvedEstimationTechnicianName,
            CreatorTechnicianName = resolvedCreatorName,
            CreatedDate = order.CreationTimestamp
                ?? quotation?.CreationTimestamp
                ?? quotationStore?.CreatedDate,
            ReservationDate = reservationDate,
            Source = NormalizeOptionalText(order.Source)
                ?? NormalizeOptionalText(quotation?.Source)
                ?? quotationStore?.Source,
            BookMethod = NormalizeOptionalText(order.BookMethod)
                ?? NormalizeOptionalText(quotation?.BookMethod)
                ?? quotationStore?.BookMethod,
            RepairDate = repairDate
        };
    }

    /// <summary>
    /// 整理車輛資訊，保留維修單上的即時資料並補上估價單識別碼。
    /// </summary>
    private static QuotationCarInfo BuildCarInfo(Order order, QuotationCarInfo? quotationCar)
    {
        var quotation = order.Quatation;

        return new QuotationCarInfo
        {
            CarUid = NormalizeOptionalText(order.CarUid)
                ?? NormalizeOptionalText(quotation?.CarUid)
                ?? quotationCar?.CarUid,
            LicensePlate = NormalizeOptionalText(order.CarNo)
                ?? NormalizeOptionalText(quotation?.CarNo)
                ?? quotationCar?.LicensePlate,
            Brand = NormalizeOptionalText(order.Brand)
                ?? NormalizeOptionalText(quotation?.Brand)
                ?? quotationCar?.Brand,
            Model = NormalizeOptionalText(order.Model)
                ?? NormalizeOptionalText(quotation?.Model)
                ?? quotationCar?.Model,
            BrandUid = NormalizeOptionalText(quotation?.BrandUid) ?? quotationCar?.BrandUid,
            ModelUid = NormalizeOptionalText(quotation?.ModelUid) ?? quotationCar?.ModelUid,
            Color = NormalizeOptionalText(order.Color)
                ?? NormalizeOptionalText(quotation?.Color)
                ?? quotationCar?.Color,
            Remark = NormalizeOptionalText(order.CarRemark)
                ?? NormalizeOptionalText(quotation?.CarRemark)
                ?? quotationCar?.Remark,
            // 里程數優先採用維修單最新紀錄，其次回退估價單或估價詳情。
            Mileage = order.Milage
                ?? quotation?.Milage
                ?? quotationCar?.Mileage
        };
    }

    /// <summary>
    /// 整理顧客資訊，維修單若有更新則優先採用。
    /// </summary>
    private static QuotationCustomerInfo BuildCustomerInfo(Order order, QuotationCustomerInfo? quotationCustomer)
    {
        var quotation = order.Quatation;

        return new QuotationCustomerInfo
        {
            CustomerUid = NormalizeOptionalText(order.CustomerUid)
                ?? NormalizeOptionalText(quotation?.CustomerUid)
                ?? quotationCustomer?.CustomerUid,
            Name = NormalizeOptionalText(order.Name)
                ?? NormalizeOptionalText(quotation?.Name)
                ?? quotationCustomer?.Name,
            Phone = NormalizeOptionalText(order.Phone)
                ?? NormalizeOptionalText(quotation?.Phone)
                ?? quotationCustomer?.Phone,
            // 電子郵件同樣以維修單資料為主，缺少時回退估價單或原始查詢結果。
            Email = NormalizeOptionalText(order.Email)
                ?? NormalizeOptionalText(quotation?.Email)
                ?? quotationCustomer?.Email,
            Gender = NormalizeOptionalText(order.Gender)
                ?? NormalizeOptionalText(quotation?.Gender)
                ?? quotationCustomer?.Gender,
            CustomerType = NormalizeOptionalText(order.CustomerType)
                ?? NormalizeOptionalText(quotation?.CustomerType)
                ?? quotationCustomer?.CustomerType,
            County = NormalizeOptionalText(order.County)
                ?? NormalizeOptionalText(quotation?.County)
                ?? quotationCustomer?.County,
            Township = NormalizeOptionalText(order.Township)
                ?? NormalizeOptionalText(quotation?.Township)
                ?? quotationCustomer?.Township,
            Reason = NormalizeOptionalText(order.Reason)
                ?? NormalizeOptionalText(quotation?.Reason)
                ?? quotationCustomer?.Reason,
            Source = NormalizeOptionalText(order.Source)
                ?? NormalizeOptionalText(quotation?.Source)
                ?? quotationCustomer?.Source,
            Remark = NormalizeOptionalText(order.ConnectRemark)
                ?? NormalizeOptionalText(quotation?.ConnectRemark)
                ?? quotationCustomer?.Remark
        };
    }

    /// <summary>
    /// 複製估價單的傷痕摘要，確保回傳資料可安全修改。
    /// </summary>
    private static List<QuotationDamageSummary> CloneDamageSummaries(List<QuotationDamageSummary>? damages)
    {
        if (damages is null || damages.Count == 0)
        {
            return new List<QuotationDamageSummary>();
        }

        var clones = new List<QuotationDamageSummary>(damages.Count);
        foreach (var damage in damages)
        {
            clones.Add(new QuotationDamageSummary
            {
                Photos = damage.Photos,
                Position = damage.Position,
                DentStatus = damage.DentStatus,
                Description = damage.Description,
                EstimatedAmount = damage.EstimatedAmount,
                FixType = damage.FixType,
                FixTypeName = damage.FixTypeName
            });
        }

        return clones;
    }

    /// <summary>
    /// 複製車體確認單資料，避免直接回傳內部參考。
    /// </summary>
    private static QuotationCarBodyConfirmationResponse? CloneCarBodyConfirmation(QuotationCarBodyConfirmationResponse? carBody)
    {
        if (carBody is null)
        {
            return null;
        }

        var markers = carBody.DamageMarkers?.Select(marker => new QuotationCarBodyDamageMarker
        {
            X = marker.X,
            Y = marker.Y,
            HasDent = marker.HasDent,
            HasScratch = marker.HasScratch,
            HasPaintPeel = marker.HasPaintPeel,
            Remark = marker.Remark
        }).ToList() ?? new List<QuotationCarBodyDamageMarker>();

        return new QuotationCarBodyConfirmationResponse
        {
            DamageMarkers = markers
        };
    }

    /// <summary>
    /// 建立維修設定資訊，將維修單最新備註與折扣同步回傳。
    /// </summary>
    private static QuotationMaintenanceDetail BuildMaintenanceDetail(Order order, QuotationMaintenanceDetail? quotationMaintenance)
    {
        var maintenance = quotationMaintenance is null
            ? new QuotationMaintenanceDetail()
            : new QuotationMaintenanceDetail
            {
                FixType = quotationMaintenance.FixType,
                FixTypeName = quotationMaintenance.FixTypeName,
                ReserveCar = quotationMaintenance.ReserveCar,
                ApplyCoating = quotationMaintenance.ApplyCoating,
                ApplyWrapping = quotationMaintenance.ApplyWrapping,
                HasRepainted = quotationMaintenance.HasRepainted,
                NeedToolEvaluation = quotationMaintenance.NeedToolEvaluation,
                OtherFee = quotationMaintenance.OtherFee,
                EstimatedRepairDays = quotationMaintenance.EstimatedRepairDays,
                EstimatedRepairHours = quotationMaintenance.EstimatedRepairHours,
                EstimatedRestorationPercentage = quotationMaintenance.EstimatedRestorationPercentage,
                SuggestedPaintReason = quotationMaintenance.SuggestedPaintReason,
                UnrepairableReason = quotationMaintenance.UnrepairableReason,
                RoundingDiscount = quotationMaintenance.RoundingDiscount,
                PercentageDiscount = quotationMaintenance.PercentageDiscount,
                DiscountReason = quotationMaintenance.DiscountReason,
                Remark = quotationMaintenance.Remark
            };

        var reserveCar = ParseBooleanFlag(order.CarReserved);
        if (reserveCar.HasValue)
        {
            maintenance.ReserveCar = reserveCar;
        }

        // 優先取出維修單儲存的純文字備註，確保回傳資料直接對應 plainRemark。
        var remark = NormalizeOptionalText(order.Content);
        if (remark is null)
        {
            // 若缺少 Content 資料則回退解析原始 Remark，確保仍可取得純文字內容。
            remark = NormalizeOptionalText(ExtractPlainRemark(order.Remark));
        }

        if (remark is not null)
        {
            maintenance.Remark = remark;
        }

        if (order.Discount.HasValue)
        {
            maintenance.RoundingDiscount = order.Discount;
        }

        if (order.DiscountPercent.HasValue)
        {
            maintenance.PercentageDiscount = order.DiscountPercent;
        }

        var discountReason = NormalizeOptionalText(order.DiscountReason);
        if (discountReason is not null)
        {
            maintenance.DiscountReason = discountReason;
        }

        var normalizedFixType = QuotationDamageFixTypeHelper.Normalize(maintenance.FixType);
        if (normalizedFixType is not null)
        {
            maintenance.FixType = normalizedFixType;
            if (string.IsNullOrWhiteSpace(maintenance.FixTypeName))
            {
                maintenance.FixTypeName = normalizedFixType;
            }
        }
        else if (!string.IsNullOrWhiteSpace(maintenance.FixType))
        {
            var resolved = QuotationDamageFixTypeHelper.ResolveDisplayName(maintenance.FixType);
            maintenance.FixType = resolved;
            if (string.IsNullOrWhiteSpace(maintenance.FixTypeName))
            {
                maintenance.FixTypeName = resolved;
            }
        }

        return maintenance;
    }

    /// <summary>
    /// 建立維修單金額資訊，保留估價、折扣與應付欄位，並沿用估價單的資料結構。
    /// </summary>
    private static QuotationAmountInfo BuildAmountInfo(Order order)
    {
        return new QuotationAmountInfo
        {
            Valuation = order.Valuation,
            Discount = order.Discount,
            DiscountPercent = order.DiscountPercent,
            Amount = order.Amount
        };
    }

    /// <summary>
    /// 建立維修狀態歷程資訊，統一整理操作人與時間。
    /// </summary>
    private static MaintenanceOrderStatusHistory BuildStatusHistory(Order order)
    {
        return new MaintenanceOrderStatusHistory
        {
            Status210Date = order.Status210Date,
            Status210User = NormalizeOptionalText(order.Status210User),
            Status220Date = order.Status220Date,
            Status220User = NormalizeOptionalText(order.Status220User),
            Status290Date = order.Status290Date,
            Status290User = NormalizeOptionalText(order.Status290User),
            Status295Date = order.Status295Timestamp,
            Status295User = NormalizeOptionalText(order.Status295User),
            CurrentStatusUser = NormalizeOptionalText(order.CurrentStatusUser)
        };
    }

    /// <summary>
    /// 由維修單現行狀態往回尋找上一個有效的狀態碼。
    /// </summary>
    private static string? ResolvePreviousOrderStatus(Order order, string currentStatus)
    {
        var history = new List<(string Code, DateTime? Timestamp)>
        {
            ("210", order.Status210Date),
            ("220", order.Status220Date),
            ("290", order.Status290Date),
            ("295", order.Status295Timestamp)
        };

        var currentIndex = history.FindIndex(item => string.Equals(item.Code, currentStatus, StringComparison.OrdinalIgnoreCase));
        if (currentIndex <= 0)
        {
            return null;
        }

        for (var i = currentIndex - 1; i >= 0; i--)
        {
            var (code, timestamp) = history[i];
            if (timestamp.HasValue || string.Equals(code, "210", StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return null;
    }

    /// <summary>
    /// 將維修單狀態回復至指定代碼並清除較晚的狀態紀錄。
    /// </summary>
    private static void ApplyOrderStatusReversion(Order order, string targetStatus, string operatorLabel, DateTime timestamp)
    {
        order.Status = targetStatus;
        order.ModificationTimestamp = timestamp;
        order.ModifiedBy = operatorLabel;
        order.CurrentStatusDate = timestamp;
        order.CurrentStatusUser = operatorLabel;

        switch (targetStatus)
        {
            case "210":
                order.Status210Date ??= timestamp;
                order.Status210User ??= operatorLabel;
                break;
            case "220":
                order.Status220Date ??= timestamp;
                order.Status220User ??= operatorLabel;
                break;
            case "290":
                order.Status290Date ??= timestamp;
                order.Status290User ??= operatorLabel;
                break;
        }

        var statusOrder = new List<string> { "210", "220", "290", "295" };
        var targetIndex = statusOrder.FindIndex(code => string.Equals(code, targetStatus, StringComparison.OrdinalIgnoreCase));

        for (var i = targetIndex + 1; i < statusOrder.Count; i++)
        {
            ClearOrderStatusRecord(order, statusOrder[i]);
        }
    }

    /// <summary>
    /// 將指定狀態的時間與操作人清空，避免保留已回溯的紀錄。
    /// </summary>
    private static void ClearOrderStatusRecord(Order order, string statusCode)
    {
        switch (statusCode)
        {
            case "220":
                order.Status220Date = null;
                order.Status220User = null;
                break;
            case "290":
                order.Status290Date = null;
                order.Status290User = null;
                break;
            case "295":
                order.Status295Timestamp = null;
                order.Status295User = null;
                break;
        }
    }

    /// <summary>
    /// 依狀態碼回傳對應的時間戳記，供回傳訊息使用。
    /// </summary>
    private static DateTime? GetOrderStatusTimestamp(Order order, string statusCode)
    {
        return statusCode switch
        {
            "210" => order.Status210Date,
            "220" => order.Status220Date,
            "290" => order.Status290Date,
            "295" => order.Status295Timestamp,
            _ => order.CurrentStatusDate
        };
    }

    /// <summary>
    /// 嘗試將文字日期轉換為 DateTime，若格式不符則回傳 null。
    /// </summary>
    private static DateTime? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// 將舊系統常見的文字旗標轉換為布林值。
    /// </summary>
    private static bool? ParseBooleanFlag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "y" or "yes" or "true" or "1" or "是" or "有" => true,
            "n" or "no" or "false" or "0" or "否" or "無" => false,
            _ => null
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
    /// 由估價單歷史狀態時間判斷上一個狀態碼。
    /// </summary>
    private static string? ResolvePreviousQuotationStatus(Quatation quotation)
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
    /// 建立續修流程使用的估價單複本，並重設建立者與狀態紀錄。
    /// </summary>
    private Quatation CloneQuotationForContinuation(
        Quatation source,
        string quotationUid,
        string quotationNo,
        int serial,
        string operatorLabel,
        DateTime timestamp)
    {
        var clone = new Quatation
        {
            QuotationUid = quotationUid,
            QuotationNo = quotationNo,
            SerialNum = serial,
            CreationTimestamp = timestamp,
            CreatedBy = operatorLabel,
            ModificationTimestamp = timestamp,
            ModifiedBy = operatorLabel,
            StoreUid = source.StoreUid,
            UserUid = source.UserUid,
            UserName = source.UserName,
            EstimationTechnicianUid = source.EstimationTechnicianUid,
            CreatorTechnicianUid = source.CreatorTechnicianUid,
            Date = DateOnly.FromDateTime(timestamp),
            Status = source.Status,
            FixType = source.FixType,
            CarUid = source.CarUid,
            CarNoInputGlobal = source.CarNoInputGlobal,
            CarNoInput = source.CarNoInput,
            CarNo = source.CarNo,
            Brand = source.Brand,
            Model = source.Model,
            BrandUid = source.BrandUid,
            ModelUid = source.ModelUid,
            Color = source.Color,
            CarRemark = source.CarRemark,
            Milage = source.Milage,
            BrandModel = source.BrandModel,
            CustomerUid = source.CustomerUid,
            CustomerType = source.CustomerType,
            PhoneInputGlobal = source.PhoneInputGlobal,
            PhoneInput = source.PhoneInput,
            Phone = source.Phone,
            Name = source.Name,
            Gender = source.Gender,
            Connect = source.Connect,
            County = source.County,
            Township = source.Township,
            Source = source.Source,
            Email = source.Email,
            Reason = source.Reason,
            ConnectRemark = source.ConnectRemark,
            Valuation = source.Valuation,
            DiscountPercent = source.DiscountPercent,
            Discount = source.Discount,
            DiscountReason = source.DiscountReason,
            BookDate = source.BookDate,
            BookMethod = source.BookMethod,
            CarReserved = source.CarReserved,
            FixDate = source.FixDate,
            ToolTest = source.ToolTest,
            Coat = source.Coat,
            Envelope = source.Envelope,
            Paint = source.Paint,
            Remark = source.Remark,
            Status110Timestamp = source.Status110Timestamp,
            Status110User = source.Status110User,
            Status180Timestamp = source.Status180Timestamp,
            Status180User = source.Status180User,
            Status190Timestamp = source.Status190Timestamp,
            Status190User = source.Status190User,
            Status191Timestamp = source.Status191Timestamp,
            Status191User = source.Status191User,
            Status199Timestamp = source.Status199Timestamp,
            Status199User = source.Status199User,
            CurrentStatusDate = timestamp,
            CurrentStatusUser = operatorLabel,
            FixExpect = source.FixExpect,
            Reject = source.Reject,
            RejectReason = source.RejectReason,
            PanelBeat = source.PanelBeat,
            PanelBeatReason = source.PanelBeatReason,
            FixTimeHour = source.FixTimeHour,
            FixTimeMin = source.FixTimeMin,
            FixExpectDay = source.FixExpectDay,
            FixExpectHour = source.FixExpectHour,
            FlagRegularCustomer = source.FlagRegularCustomer
        };

        var statusCode = NormalizeOptionalText(source.Status);
        if (statusCode is not null)
        {
            switch (statusCode)
            {
                case "110":
                    clone.Status110Timestamp = timestamp;
                    clone.Status110User = operatorLabel;
                    break;
                case "180":
                    clone.Status180Timestamp = timestamp;
                    clone.Status180User = operatorLabel;
                    break;
                case "190":
                    clone.Status190Timestamp = timestamp;
                    clone.Status190User = operatorLabel;
                    break;
                case "191":
                    clone.Status191Timestamp = timestamp;
                    clone.Status191User = operatorLabel;
                    break;
                case "195":
                    clone.Status199Timestamp = timestamp;
                    clone.Status199User = operatorLabel;
                    break;
            }
        }

        return clone;
    }

    /// <summary>
    /// 嘗試複製舊照片並回傳舊新 PhotoUID 的對照表。
    /// </summary>
    private async Task<Dictionary<string, string>> DuplicatePhotosForContinuationAsync(
        string? sourceQuotationUid,
        string targetQuotationUid,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSourceQuotationUid = NormalizeOptionalText(sourceQuotationUid);
        if (normalizedSourceQuotationUid is null)
        {
            return result;
        }

        var photos = await _dbContext.PhotoData
            .Where(photo => photo.QuotationUid == normalizedSourceQuotationUid)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return result;
        }

        var storageRoot = EnsurePhotoStorageRoot();
        foreach (var photo in photos)
        {
            var oldPhotoUid = NormalizeOptionalText(photo.PhotoUid);
            if (oldPhotoUid is null)
            {
                continue;
            }

            var newPhotoUid = BuildPhotoUid();
            if (!TryClonePhotoFile(storageRoot, oldPhotoUid, newPhotoUid))
            {
                _logger.LogWarning(
                    "續修維修單複製照片 {PhotoUid} 失敗，將沿用舊圖片供參考。",
                    oldPhotoUid);
                continue;
            }

            var clone = new PhotoDatum
            {
                PhotoUid = newPhotoUid,
                QuotationUid = targetQuotationUid,
                RelatedUid = photo.RelatedUid,
                Posion = photo.Posion,
                Comment = photo.Comment,
                PhotoShape = photo.PhotoShape,
                PhotoShapeOther = photo.PhotoShapeOther,
                PhotoShapeShow = photo.PhotoShapeShow,
                Cost = photo.Cost,
                FlagFinish = photo.FlagFinish,
                FinishCost = photo.FinishCost
            };

            await _dbContext.PhotoData.AddAsync(clone, cancellationToken);
            result[oldPhotoUid] = newPhotoUid;
        }

        return result;
    }

    /// <summary>
    /// 將 remark 內的舊 PhotoUID 取代為新的識別碼。
    /// </summary>
    private static string? ReplacePhotoUids(string? remark, IReadOnlyDictionary<string, string> photoUidMap)
    {
        if (string.IsNullOrWhiteSpace(remark) || photoUidMap.Count == 0)
        {
            return remark;
        }

        var updated = remark;
        foreach (var pair in photoUidMap)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            updated = updated.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return updated;
    }

    /// <summary>
    /// 產生新的照片識別碼，維持 Ph_ 前綴格式。
    /// </summary>
    private static string BuildPhotoUid()
    {
        return $"Ph_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 建立續修估價單唯一識別碼，維持 Q_ 前綴格式。
    /// </summary>
    private static string BuildQuotationUid()
    {
        return $"Q_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 依建立時間產生新的估價單編號。
    /// </summary>
    private static string BuildQuotationNo(int serial, DateTime timestamp)
    {
        return $"Q{timestamp:yyMM}{serial:0000}";
    }

    /// <summary>
    /// 取得實際儲存照片的根目錄，若不存在則自動建立。
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
    /// 透過 PhotoUID 推算實際的檔案路徑。
    /// </summary>
    private string ResolvePhotoPhysicalPath(string storageRoot, string photoUid)
    {
        var matched = Directory.EnumerateFiles(storageRoot, photoUid + ".*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(matched)
            ? matched
            : Path.Combine(storageRoot, photoUid);
    }

    /// <summary>
    /// 嘗試複製舊照片的實體檔案為新的 PhotoUID。
    /// </summary>
    private bool TryClonePhotoFile(string storageRoot, string sourcePhotoUid, string targetPhotoUid)
    {
        try
        {
            var sourcePath = ResolvePhotoPhysicalPath(storageRoot, sourcePhotoUid);
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var destinationPath = Path.Combine(storageRoot, targetPhotoUid + extension);
            File.Copy(sourcePath, destinationPath, overwrite: false);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "續修維修單複製照片 {PhotoUid} 時發生 IO 例外。", sourcePhotoUid);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "續修維修單複製照片 {PhotoUid} 時遭拒存取。", sourcePhotoUid);
            return false;
        }
    }

    /// <summary>
    /// 續修流程取得下一個估價單序號，沿用每月遞增規則。
    /// </summary>
    private async Task<int> GenerateNextQuotationSerialAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        var prefix = $"Q{timestamp:yyMM}";

        var prefixCandidates = await _dbContext.Quatations
            .AsNoTracking()
            .Where(quotation => !string.IsNullOrEmpty(quotation.QuotationNo) && EF.Functions.Like(quotation.QuotationNo!, prefix + "%"))
            .OrderByDescending(quotation => quotation.SerialNum)
            .ThenByDescending(quotation => quotation.QuotationNo)
            .Select(quotation => new SerialCandidate(quotation.SerialNum, quotation.QuotationNo))
            .Take(SerialCandidateFetchCount)
            .ToListAsync(cancellationToken);

        var maxSerial = ExtractMaxSerial(prefixCandidates, prefix);

        if (maxSerial == 0)
        {
            var monthStart = new DateTime(timestamp.Year, timestamp.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthCandidates = await _dbContext.Quatations
                .AsNoTracking()
                .Where(quotation => quotation.CreationTimestamp >= monthStart && quotation.CreationTimestamp < monthEnd)
                .OrderByDescending(quotation => quotation.SerialNum)
                .ThenByDescending(quotation => quotation.QuotationNo)
                .Select(quotation => new SerialCandidate(quotation.SerialNum, quotation.QuotationNo))
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
