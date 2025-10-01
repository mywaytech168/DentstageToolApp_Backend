using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Customers;
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
    public async Task<CustomerPhoneSearchResponse> SearchByPhoneAsync(
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

        _logger.LogInformation("執行電話搜尋，關鍵字：{Phone}，純數字：{Digits}。", normalizedPhone, phoneDigits);

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

        // ---------- 查詢相關維修單 ----------
        var relatedOrders = await FetchRelatedOrdersAsync(
            normalizedPhone,
            phoneDigits,
            customerEntities,
            cancellationToken);

        // ---------- 組裝回應 ----------
        var customerItems = customerEntities
            .Select(MapToCustomerItem)
            .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
            .ToList();

        var summary = BuildMaintenanceSummary(relatedOrders);

        var response = new CustomerPhoneSearchResponse
        {
            QueryPhone = normalizedPhone,
            QueryDigits = phoneDigits,
            Customers = customerItems,
            MaintenanceSummary = summary,
            Message = BuildMessage(customerItems.Count, summary.TotalOrders)
        };

        _logger.LogInformation(
            "電話搜尋完成，找到 {CustomerCount} 位客戶與 {OrderCount} 筆維修單。",
            customerItems.Count,
            summary.TotalOrders);

        return response;
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 以電話號碼與客戶清單取得相關維修單資料。
    /// </summary>
    private async Task<List<Order>> FetchRelatedOrdersAsync(
        string normalizedPhone,
        string phoneDigits,
        IReadOnlyCollection<Customer> customerEntities,
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
    /// 將資料庫客戶實體轉為 API 回傳模型。
    /// </summary>
    private static CustomerPhoneSearchItem MapToCustomerItem(Customer customer)
    {
        return new CustomerPhoneSearchItem
        {
            CustomerUid = customer.CustomerUid,
            CustomerName = customer.Name,
            Phone = customer.Phone,
            Category = customer.CustomerType,
            Gender = customer.Gender,
            County = customer.County,
            Township = customer.Township,
            Source = customer.Source,
            Remark = customer.ConnectRemark,
            CreatedAt = customer.CreationTimestamp,
            ModifiedAt = customer.ModificationTimestamp
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

    // ---------- 生命週期 ----------
    // 服務由 DI 容器管理，無額外生命週期實作需求。
}
