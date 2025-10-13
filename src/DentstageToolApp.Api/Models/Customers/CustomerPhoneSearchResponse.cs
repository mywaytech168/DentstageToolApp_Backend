using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 電話搜尋 API 的回傳模型，包含客戶清單與維修統計資訊。
/// </summary>
public class CustomerPhoneSearchResponse
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
    /// 查詢到的客戶清單。
    /// </summary>
    public IReadOnlyCollection<CustomerPhoneSearchItem> Customers { get; set; } =
        Array.Empty<CustomerPhoneSearchItem>();

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
