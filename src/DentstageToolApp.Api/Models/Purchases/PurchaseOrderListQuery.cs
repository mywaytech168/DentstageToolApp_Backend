using System;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單列表查詢參數，支援分頁、店鋪關鍵字與日期區間條件。
/// </summary>
public class PurchaseOrderListQuery : PaginationRequest
{
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
