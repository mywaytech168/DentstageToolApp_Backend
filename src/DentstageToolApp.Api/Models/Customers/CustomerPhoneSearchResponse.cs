using System;
using System.Collections.Generic;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Models.Quotations;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 電話搜尋 API 的基底回傳模型，提供查詢條件與摘要資料。
/// </summary>
/// <typeparam name="TItem">客戶清單的資料型別。</typeparam>
public class CustomerPhoneSearchResponseBase<TItem>
{
    /// <summary>
    /// 前端輸入的查詢電話，經過去除前後空白後的結果，方便確認查詢條件。
    /// </summary>
    public string QueryPhone { get; set; } = string.Empty;

    /// <summary>
    /// 將電話轉為純數字後的結果，對應資料庫 PhoneQuery 欄位。
    /// </summary>
    public string QueryDigits { get; set; } = string.Empty;

    /// <summary>
    /// 查詢到的客戶資料清單，依建立時間倒序排列，方便前端依需求挑選對應客戶。
    /// </summary>
    public IReadOnlyCollection<TItem> Customers { get; set; }
        = Array.Empty<TItem>();

    /// <summary>
    /// 與電話相關的維修紀錄統計資訊。
    /// </summary>
    public CustomerMaintenanceSummary MaintenanceSummary { get; set; } =
        new CustomerMaintenanceSummary();

    /// <summary>
    /// 供前端呈現的人性化訊息，例如是否查到客戶或維修紀錄。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 電話搜尋 API 的標準回傳模型，提供基本客戶資訊。
/// </summary>
public class CustomerPhoneSearchResponse : CustomerPhoneSearchResponseBase<CustomerPhoneSearchItem>
{
}

/// <summary>
/// 電話搜尋 API 的詳細回傳模型，補充估價單與維修單清單。
/// </summary>
public class CustomerPhoneSearchDetailResponse : CustomerPhoneSearchResponseBase<CustomerPhoneSearchDetailItem>
{
}

/// <summary>
/// 電話搜尋回傳的單筆客戶資訊，提供前端顯示與後續操作。
/// </summary>
public class CustomerPhoneSearchItem
{
    /// <summary>
    /// 客戶唯一識別碼，方便後續建立估價或維修單時引用。
    /// </summary>
    public string CustomerUid { get; set; } = string.Empty;

    /// <summary>
    /// 客戶姓名。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 客戶電子郵件，提供客服透過信箱聯繫或確認帳號資訊。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 客戶分類，例如一般客戶或企業客戶。
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 客戶性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 所在縣市。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 所在鄉鎮區。
    /// </summary>
    public string? Township { get; set; }

    /// <summary>
    /// 消息來源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 額外備註資訊。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 客戶資料建立時間，方便顯示客戶建立歷程。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 客戶資料最後修改時間。
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// 電話搜尋回傳的單筆客戶詳細資訊，包含估價單與維修單清單。
/// </summary>
public class CustomerPhoneSearchDetailItem : CustomerPhoneSearchItem
{
    /// <summary>
    /// 與客戶相關的估價單清單，依建立時間倒序排列。
    /// </summary>
    public IReadOnlyCollection<QuotationSummaryResponse> Quotations { get; set; }
        = Array.Empty<QuotationSummaryResponse>();

    /// <summary>
    /// 與客戶相關的維修單清單，依建立時間倒序排列。
    /// </summary>
    public IReadOnlyCollection<MaintenanceOrderSummaryResponse> MaintenanceOrders { get; set; }
        = Array.Empty<MaintenanceOrderSummaryResponse>();
}

/// <summary>
/// 與指定電話相關的維修統計資訊，提供取消與預約次數。
/// </summary>
public class CustomerMaintenanceSummary
{
    /// <summary>
    /// 相關維修單總筆數，包含所有狀態。
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// 狀態屬於預約的維修單數量。
    /// </summary>
    public int ReservationCount { get; set; }

    /// <summary>
    /// 狀態屬於取消或終止的維修單數量。
    /// </summary>
    public int CancellationCount { get; set; }

    /// <summary>
    /// 是否有任何維修紀錄，供前端快速判斷是否需要顯示歷史。
    /// </summary>
    public bool HasMaintenanceHistory { get; set; }
}
