using System;
using System.ComponentModel.DataAnnotations;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單列表查詢參數，支援分頁、門市 Token（StoreUID）、店鋪關鍵字與日期區間條件。
/// </summary>
public class PurchaseOrderListQuery : PaginationRequest
{
    /// <summary>
    /// 門市識別碼，對應門市本身的 Token（StoreUID），用於鎖定特定門市資料。
    /// </summary>
    [StringLength(100, ErrorMessage = "門市識別碼長度不可超過 100 個字元。")]
    public string? StoreUid { get; set; }

    /// <summary>
    /// 店鋪名稱關鍵字，將於資料庫側以模糊搜尋比對。
    /// </summary>
    public string? StoreKeyword { get; set; }

    /// <summary>
    /// 查詢起始日期，僅回傳採購日期大於等於此日期的資料。
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// 查詢結束日期，僅回傳採購日期小於等於此日期的資料。
    /// </summary>
    public DateOnly? EndDate { get; set; }
}
