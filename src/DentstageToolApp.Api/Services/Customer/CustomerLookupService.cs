using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Customers;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Models.Pagination;
using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Services.Shared;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶電話查詢服務實作，負責整理搜尋條件並回傳統計資訊。
/// </summary>
public class CustomerLookupService : ICustomerLookupService
{
    private static readonly HashSet<string> ReservationStatusCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "210",
        "220"
    };

    private static readonly HashSet<string> CancellationStatusCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "290",
        "295"
    };

    private static readonly string[] ReservationKeywords =
    {
        "預約",
        "預定",
        "排程"
    };

    private static readonly string[] CancellationKeywords =
    {
        "取消",
        "終止",
        "作廢"
    };

    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CustomerLookupService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public CustomerLookupService(
        DentstageToolAppContext dbContext,
        ILogger<CustomerLookupService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CustomerListResponse> GetCustomersAsync(PaginationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pagination = request ?? new PaginationRequest();
        var (page, pageSize) = pagination.Normalize();

        _logger.LogInformation(
            "查詢客戶列表，頁碼：{Page}，每頁筆數：{PageSize}。",
            page,
            pageSize);

        var items = await _dbContext.Customers
            .AsNoTracking()
            .OrderByDescending(customer => customer.CreationTimestamp)
            .ThenBy(customer => customer.CustomerUid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new CustomerListItem
            {
                CustomerUid = customer.CustomerUid,
                CustomerName = customer.Name,
                Phone = customer.Phone,
                Category = customer.CustomerType,
                Source = customer.Source,
                CreatedAt = customer.CreationTimestamp
            })
            .ToListAsync(cancellationToken);

        var totalCount = await _dbContext.Customers.CountAsync(cancellationToken);

        _logger.LogInformation(
            "客戶列表查詢完成，頁碼：{Page}，共取得 {Count} / {Total} 筆資料。",
            page,
            items.Count,
            totalCount);

        return new CustomerListResponse
        {
            Items = items,
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            }
        };
    }

    /// <inheritdoc />
    public async Task<CustomerDetailResponse> GetCustomerAsync(string customerUid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedUid = (customerUid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUid))
        {
            throw new CustomerLookupException(HttpStatusCode.BadRequest, "請提供客戶識別碼。");
        }

        _logger.LogInformation("查詢客戶詳細資料，UID：{CustomerUid}。", normalizedUid);

        var entity = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.CustomerUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            throw new CustomerLookupException(HttpStatusCode.NotFound, "找不到對應的客戶資料。");
        }

        return new CustomerDetailResponse
        {
            CustomerUid = entity.CustomerUid,
            CustomerName = entity.Name,
            Category = entity.CustomerType,
            Gender = entity.Gender,
            Connect = entity.Connect,
            Phone = entity.Phone,
            Email = entity.Email,
            County = entity.County,
            Township = entity.Township,
            Source = entity.Source,
            Reason = entity.Reason,
            Remark = entity.ConnectRemark,
            CreatedAt = entity.CreationTimestamp,
            UpdatedAt = entity.ModificationTimestamp,
            CreatedBy = entity.CreatedBy,
            ModifiedBy = entity.ModifiedBy
        };
    }

    /// <inheritdoc />
    public async Task<CustomerPhoneSearchResponse> SearchByPhoneAsync(
        CustomerPhoneSearchRequest request,
        CancellationToken cancellationToken)
    {
        // 先取得完整的候選結果，再轉換為精簡版本，避免重複查詢資料庫。
        var context = await SearchCustomerDetailContextAsync(request, cancellationToken);

        var simpleCustomers = context.Customers
            .Select(ConvertToSimpleItem)
            .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
            .ToList();

        return new CustomerPhoneSearchResponse
        {
            QueryPhone = context.QueryPhone,
            QueryDigits = context.QueryDigits,
            Customers = simpleCustomers,
            MaintenanceSummary = context.MaintenanceSummary,
            Message = context.Message
        };
    }

    /// <inheritdoc />
    public async Task<CustomerPhoneSearchDetailResponse> SearchCustomerWithDetailsAsync(
        CustomerPhoneSearchRequest request,
        CancellationToken cancellationToken)
    {
        // 共用候選查詢邏輯，確保精簡版與詳細版結果一致。
        var context = await SearchCustomerDetailContextAsync(request, cancellationToken);

        var customer = context.Customers.FirstOrDefault();

        if (customer is null)
        {
            _logger.LogInformation("電話搜尋完成（含歷史），未找到符合條件的客戶。");
        }
        else if (context.Customers.Count > 1)
        {
            _logger.LogInformation(
                "電話搜尋完成（含歷史），存在多筆客戶資料，預設取最新一筆回傳。累計筆數：{CustomerCount}。",
                context.Customers.Count);
        }

        return new CustomerPhoneSearchDetailResponse
        {
            QueryPhone = context.QueryPhone,
            QueryDigits = context.QueryDigits,
            Customer = customer,
            MaintenanceSummary = context.MaintenanceSummary,
            Message = context.Message
        };
    }

    /// <summary>
    /// 取得電話搜尋的候選清單，提供詳細與精簡兩種查詢共用。
    /// </summary>
    private async Task<CustomerPhoneSearchDetailContext> SearchCustomerDetailContextAsync(
        CustomerPhoneSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CustomerLookupException(HttpStatusCode.BadRequest, "請提供查詢條件。");
        }

        // ---------- 參數整理區 ----------
        var normalizedPhone = NormalizePhone(request.Phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw new CustomerLookupException(HttpStatusCode.BadRequest, "請輸入欲查詢的電話號碼。");
        }

        var phoneDigits = ExtractDigits(normalizedPhone);
        var digitsPattern = string.IsNullOrEmpty(phoneDigits) ? null : $"%{phoneDigits}%";
        var rawPattern = $"%{normalizedPhone}%";

        _logger.LogInformation("執行電話搜尋（含歷史），關鍵字：{Phone}，純數字：{Digits}。", normalizedPhone, phoneDigits);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 查詢客戶資料 ----------
        var customersQuery = _dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrEmpty(digitsPattern))
        {
            customersQuery = customersQuery.Where(customer =>
                (customer.PhoneQuery != null && EF.Functions.Like(customer.PhoneQuery, digitsPattern))
                || (customer.Phone != null && EF.Functions.Like(customer.Phone, rawPattern)));
        }
        else
        {
            customersQuery = customersQuery.Where(customer =>
                customer.Phone != null && EF.Functions.Like(customer.Phone, rawPattern));
        }

        var customerEntities = await customersQuery.ToListAsync(cancellationToken);

        // ---------- 查詢相關估價單與維修單 ----------
        var relatedQuotations = await FetchRelatedQuotationsAsync(
            normalizedPhone,
            phoneDigits,
            customerEntities,
            cancellationToken);

        var relatedOrders = await FetchRelatedOrdersAsync(
            normalizedPhone,
            phoneDigits,
            customerEntities,
            cancellationToken);

        // ---------- 摘要資料整理 ----------
        var quotationSummaries = await SummaryMappingHelper.BuildQuotationSummariesAsync(
            _dbContext,
            relatedQuotations,
            cancellationToken);

        var maintenanceSummaries = await SummaryMappingHelper.BuildMaintenanceSummariesAsync(
            _dbContext,
            relatedOrders,
            cancellationToken);

        var quotationMap = BuildCustomerQuotationMap(relatedQuotations, quotationSummaries);
        var orderMap = BuildCustomerOrderMap(relatedOrders, maintenanceSummaries);

        // ---------- 組裝回應 ----------
        var customerItems = customerEntities
            .Select(customer => MapToCustomerDetailItem(customer, quotationMap, orderMap))
            .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
            .ToList();

        var summary = BuildMaintenanceSummary(relatedOrders);

        var message = BuildMessage(customerItems.Count, summary.TotalOrders);

        _logger.LogInformation(
            "電話搜尋完成（含歷史），找到 {CustomerCount} 位客戶與 {OrderCount} 筆維修單。",
            customerItems.Count,
            summary.TotalOrders);

        return new CustomerPhoneSearchDetailContext
        {
            QueryPhone = normalizedPhone,
            QueryDigits = phoneDigits,
            Customers = customerItems,
            MaintenanceSummary = summary,
            Message = message
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 以電話號碼與客戶清單取得相關估價單資料。
    /// </summary>
    private async Task<List<Quatation>> FetchRelatedQuotationsAsync(
        string normalizedPhone,
        string phoneDigits,
        IReadOnlyCollection<DentstageToolApp.Infrastructure.Entities.Customer> customerEntities,
        CancellationToken cancellationToken)
    {
        var quotations = new Dictionary<string, Quatation>(StringComparer.OrdinalIgnoreCase);

        var rawPattern = $"%{normalizedPhone}%";
        var quotationsByRawPhone = await _dbContext.Quatations
            .AsNoTracking()
            .Where(quotation =>
                (quotation.Phone != null && EF.Functions.Like(quotation.Phone, rawPattern))
                || (quotation.PhoneInput != null && EF.Functions.Like(quotation.PhoneInput, rawPattern))
                || (quotation.PhoneInputGlobal != null && EF.Functions.Like(quotation.PhoneInputGlobal, rawPattern)))
            .ToListAsync(cancellationToken);

        MergeQuotations(quotations, quotationsByRawPhone);

        if (!string.IsNullOrEmpty(phoneDigits))
        {
            var digitsPattern = $"%{phoneDigits}%";
            var quotationsByDigits = await _dbContext.Quatations
                .AsNoTracking()
                .Where(quotation =>
                    (quotation.Phone != null && EF.Functions.Like(quotation.Phone, digitsPattern))
                    || (quotation.PhoneInput != null && EF.Functions.Like(quotation.PhoneInput, digitsPattern))
                    || (quotation.PhoneInputGlobal != null && EF.Functions.Like(quotation.PhoneInputGlobal, digitsPattern)))
                .ToListAsync(cancellationToken);

            MergeQuotations(quotations, quotationsByDigits);
        }

        if (customerEntities.Count > 0)
        {
            var customerUids = customerEntities
                .Select(customer => NormalizeOptionalText(customer.CustomerUid))
                .Where(uid => uid is not null)
                .Select(uid => uid!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (customerUids.Count > 0)
            {
                var quotationsByCustomer = await _dbContext.Quatations
                    .AsNoTracking()
                    .Where(quotation =>
                        quotation.CustomerUid != null && customerUids.Contains(quotation.CustomerUid))
                    .ToListAsync(cancellationToken);

                MergeQuotations(quotations, quotationsByCustomer);
            }
        }

        return quotations
            .Values
            .OrderByDescending(quotation => quotation.CreationTimestamp ?? DateTime.MinValue)
            .ThenByDescending(quotation => quotation.QuotationNo)
            .ToList();
    }

    /// <summary>
    /// 以電話號碼與客戶清單取得相關維修單資料。
    /// </summary>
    private async Task<List<Order>> FetchRelatedOrdersAsync(
        string normalizedPhone,
        string phoneDigits,
        IReadOnlyCollection<DentstageToolApp.Infrastructure.Entities.Customer> customerEntities,
        CancellationToken cancellationToken)
    {
        var orders = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);

        // 先以原始電話進行模糊查詢，包含 Phone、PhoneInput 與 PhoneInputGlobal。
        var rawPattern = $"%{normalizedPhone}%";
        var ordersByRawPhone = await _dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                (order.Phone != null && EF.Functions.Like(order.Phone, rawPattern))
                || (order.PhoneInput != null && EF.Functions.Like(order.PhoneInput, rawPattern))
                || (order.PhoneInputGlobal != null && EF.Functions.Like(order.PhoneInputGlobal, rawPattern)))
            .ToListAsync(cancellationToken);

        MergeOrders(orders, ordersByRawPhone);

        // 若有純數字形式的電話，額外再以數字欄位模糊比對。
        if (!string.IsNullOrEmpty(phoneDigits))
        {
            var digitsPattern = $"%{phoneDigits}%";
            var ordersByDigits = await _dbContext.Orders
                .AsNoTracking()
                .Where(order =>
                    (order.Phone != null && EF.Functions.Like(order.Phone, digitsPattern))
                    || (order.PhoneInput != null && EF.Functions.Like(order.PhoneInput, digitsPattern))
                    || (order.PhoneInputGlobal != null && EF.Functions.Like(order.PhoneInputGlobal, digitsPattern)))
                .ToListAsync(cancellationToken);

            MergeOrders(orders, ordersByDigits);
        }

        // 針對找到的客戶，再以 CustomerUid 反查所有相關維修單。
        if (customerEntities.Count > 0)
        {
            var customerUids = customerEntities
                .Where(customer => !string.IsNullOrWhiteSpace(customer.CustomerUid))
                .Select(customer => customer.CustomerUid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (customerUids.Count > 0)
            {
                var ordersByCustomer = await _dbContext.Orders
                    .AsNoTracking()
                    .Where(order =>
                        order.CustomerUid != null && customerUids.Contains(order.CustomerUid))
                    .ToListAsync(cancellationToken);

                MergeOrders(orders, ordersByCustomer);
            }
        }

        return orders
            .Values
            .OrderByDescending(order => order.CreationTimestamp ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>
    /// 合併維修單集合，避免重複加入相同工單。
    /// </summary>
    private static void MergeOrders(IDictionary<string, Order> target, IEnumerable<Order> source)
    {
        foreach (var order in source)
        {
            if (string.IsNullOrWhiteSpace(order.OrderUid))
            {
                continue;
            }

            if (target.ContainsKey(order.OrderUid))
            {
                continue;
            }

            target[order.OrderUid] = order;
        }
    }

    /// <summary>
    /// 合併估價單集合，避免重複加入相同報價單。
    /// </summary>
    private static void MergeQuotations(IDictionary<string, Quatation> target, IEnumerable<Quatation> source)
    {
        foreach (var quotation in source)
        {
            if (string.IsNullOrWhiteSpace(quotation.QuotationUid))
            {
                continue;
            }

            if (target.ContainsKey(quotation.QuotationUid))
            {
                continue;
            }

            target[quotation.QuotationUid] = quotation;
        }
    }

    /// <summary>
    /// 建立客戶對估價單摘要的對照表，方便後續直接取用。
    /// </summary>
    private static IReadOnlyDictionary<string, List<QuotationSummaryResponse>> BuildCustomerQuotationMap(
        IReadOnlyList<Quatation> quotations,
        IReadOnlyList<QuotationSummaryResponse> summaries)
    {
        var map = new Dictionary<string, List<QuotationSummaryResponse>>(StringComparer.OrdinalIgnoreCase);
        var count = Math.Min(quotations.Count, summaries.Count);

        for (var i = 0; i < count; i++)
        {
            var quotation = quotations[i];
            var summary = summaries[i];
            var normalizedUid = NormalizeOptionalText(quotation.CustomerUid);
            if (normalizedUid is null)
            {
                continue;
            }

            if (!map.TryGetValue(normalizedUid, out var list))
            {
                list = new List<QuotationSummaryResponse>();
                map[normalizedUid] = list;
            }

            list.Add(summary);
        }

        foreach (var key in map.Keys.ToList())
        {
            map[key] = map[key]
                .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(item => item.QuotationNo)
                .ToList();
        }

        return map;
    }

    /// <summary>
    /// 建立客戶對維修單摘要的對照表。
    /// </summary>
    private static IReadOnlyDictionary<string, List<MaintenanceOrderSummaryResponse>> BuildCustomerOrderMap(
        IReadOnlyList<Order> orders,
        IReadOnlyList<MaintenanceOrderSummaryResponse> summaries)
    {
        var map = new Dictionary<string, List<MaintenanceOrderSummaryResponse>>(StringComparer.OrdinalIgnoreCase);
        var count = Math.Min(orders.Count, summaries.Count);

        for (var i = 0; i < count; i++)
        {
            var order = orders[i];
            var summary = summaries[i];
            var normalizedUid = NormalizeOptionalText(order.CustomerUid);
            if (normalizedUid is null)
            {
                continue;
            }

            if (!map.TryGetValue(normalizedUid, out var list))
            {
                list = new List<MaintenanceOrderSummaryResponse>();
                map[normalizedUid] = list;
            }

            list.Add(summary);
        }

        foreach (var key in map.Keys.ToList())
        {
            map[key] = map[key]
                .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(item => item.OrderNo)
                .ToList();
        }

        return map;
    }

    private static CustomerPhoneSearchDetailItem MapToCustomerDetailItem(
        DentstageToolApp.Infrastructure.Entities.Customer customer,
        IReadOnlyDictionary<string, List<QuotationSummaryResponse>> quotationMap,
        IReadOnlyDictionary<string, List<MaintenanceOrderSummaryResponse>> orderMap)
    {
        var normalizedUid = NormalizeOptionalText(customer.CustomerUid);

        var quotations = normalizedUid is not null &&
            quotationMap.TryGetValue(normalizedUid, out var quotationList)
            ? (IReadOnlyCollection<QuotationSummaryResponse>)quotationList
            : Array.Empty<QuotationSummaryResponse>();

        var orders = normalizedUid is not null &&
            orderMap.TryGetValue(normalizedUid, out var orderList)
            ? (IReadOnlyCollection<MaintenanceOrderSummaryResponse>)orderList
            : Array.Empty<MaintenanceOrderSummaryResponse>();

        return new CustomerPhoneSearchDetailItem
        {
            CustomerUid = customer.CustomerUid,
            CustomerName = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Category = customer.CustomerType,
            Gender = customer.Gender,
            County = customer.County,
            Township = customer.Township,
            Source = customer.Source,
            Remark = customer.ConnectRemark,
            CreatedAt = customer.CreationTimestamp,
            ModifiedAt = customer.ModificationTimestamp,
            Quotations = quotations,
            MaintenanceOrders = orders
        };
    }

    /// <summary>
    /// 將包含歷史資料的客戶項目轉換成精簡版本，提供無歷史資料需求的端點使用。
    /// </summary>
    private static CustomerPhoneSearchItem ConvertToSimpleItem(CustomerPhoneSearchDetailItem detailItem)
    {
        return new CustomerPhoneSearchItem
        {
            CustomerUid = detailItem.CustomerUid,
            CustomerName = detailItem.CustomerName,
            Phone = detailItem.Phone,
            Email = detailItem.Email,
            Category = detailItem.Category,
            Gender = detailItem.Gender,
            County = detailItem.County,
            Township = detailItem.Township,
            Source = detailItem.Source,
            Remark = detailItem.Remark,
            CreatedAt = detailItem.CreatedAt,
            ModifiedAt = detailItem.ModifiedAt
        };
    }

    /// <summary>
    /// 依據維修單集合計算取消與預約次數。
    /// </summary>
    private static CustomerMaintenanceSummary BuildMaintenanceSummary(IReadOnlyCollection<Order> orders)
    {
        if (orders.Count == 0)
        {
            return new CustomerMaintenanceSummary
            {
                TotalOrders = 0,
                ReservationCount = 0,
                CancellationCount = 0,
                HasMaintenanceHistory = false
            };
        }

        var reservationCount = 0;
        var cancellationCount = 0;

        foreach (var order in orders)
        {
            var status = order.Status;
            if (string.IsNullOrWhiteSpace(status))
            {
                continue;
            }

            if (IsCancellationStatus(status))
            {
                cancellationCount++;
                continue;
            }

            if (IsReservationStatus(status))
            {
                reservationCount++;
            }
        }

        return new CustomerMaintenanceSummary
        {
            TotalOrders = orders.Count,
            ReservationCount = reservationCount,
            CancellationCount = cancellationCount,
            HasMaintenanceHistory = true
        };
    }

    /// <summary>
    /// 建立回傳訊息，提供前端顯示文字。
    /// </summary>
    private static string BuildMessage(int customerCount, int orderCount)
    {
        if (customerCount == 0 && orderCount == 0)
        {
            return "查無符合的客戶與維修紀錄。";
        }

        if (customerCount == 0)
        {
            return "查無客戶資料，但已回傳相關維修紀錄統計供參考。";
        }

        if (orderCount == 0)
        {
            return "已找到客戶資料，目前尚無維修紀錄。";
        }

        return "查詢成功，已回傳客戶資料與維修統計。";
    }

    /// <summary>
    /// 判斷狀態是否屬於取消或終止。
    /// </summary>
    private static bool IsCancellationStatus(string status)
    {
        var trimmed = status.Trim();

        if (CancellationStatusCodes.Contains(trimmed))
        {
            return true;
        }

        return CancellationKeywords.Any(keyword =>
            trimmed.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判斷狀態是否屬於預約相關狀態。
    /// </summary>
    private static bool IsReservationStatus(string status)
    {
        var trimmed = status.Trim();

        if (ReservationStatusCodes.Contains(trimmed))
        {
            return true;
        }

        return ReservationKeywords.Any(keyword =>
            trimmed.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 去除電話前後空白並轉換全形數字為半形，保持統一格式。
    /// </summary>
    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var trimmed = phone.Trim();
        var buffer = new char[trimmed.Length];

        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];

            if (ch >= '０' && ch <= '９')
            {
                buffer[i] = (char)('0' + (ch - '０'));
                continue;
            }

            if (ch == '＋')
            {
                buffer[i] = '+';
                continue;
            }

            buffer[i] = ch;
        }

        return new string(buffer);
    }

    /// <summary>
    /// 取出電話中的數字字元，供 PhoneQuery 比對使用。
    /// </summary>
    private static string ExtractDigits(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits;
    }

    /// <summary>
    /// 正規化可選文字欄位，將空白字串轉換為 null，方便後續比對。
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
    /// 電話搜尋候選資料的共用承載物件，提供服務層重複利用。
    /// </summary>
    private sealed class CustomerPhoneSearchDetailContext
    {
        /// <summary>
        /// 標準化後的查詢電話號碼。
        /// </summary>
        public string QueryPhone { get; init; } = string.Empty;

        /// <summary>
        /// 僅保留數字的電話比對字串。
        /// </summary>
        public string QueryDigits { get; init; } = string.Empty;

        /// <summary>
        /// 找到的客戶候選清單，依建立時間倒序排列。
        /// </summary>
        public IReadOnlyList<CustomerPhoneSearchDetailItem> Customers { get; init; }
            = Array.Empty<CustomerPhoneSearchDetailItem>();

        /// <summary>
        /// 與電話相關的維修統計資訊。
        /// </summary>
        public CustomerMaintenanceSummary MaintenanceSummary { get; init; }
            = new CustomerMaintenanceSummary();

        /// <summary>
        /// 操作提示訊息，說明查詢成果。
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }

    // ---------- 生命週期 ----------
    // 服務由 DI 容器管理，無額外生命週期實作需求。
}
