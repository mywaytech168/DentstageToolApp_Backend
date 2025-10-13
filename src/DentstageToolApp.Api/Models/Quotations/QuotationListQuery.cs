using System;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 估價單列表查詢參數，對應前端查詢條件與分頁設定。
/// </summary>
public class QuotationListQuery
{
    /// <summary>
    /// 指定維修類型，優先傳入維修類型主檔識別碼，亦保留舊有文字對應。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 指定估價單狀態碼，對應資料表欄位 Status。 ALL->(null) 110估價中(編輯中) 180估價完成 190已預約 191轉維修(待維修) 195估價或預約取消。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 查詢開始日期，將比對建立時間的下限。
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// 查詢結束日期，將比對建立時間的上限（包含當日）。
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// 客戶關鍵字，可搜尋姓名或電話。
    /// </summary>
    public string? CustomerKeyword { get; set; }

    /// <summary>
    /// 車牌關鍵字，可模糊搜尋車牌號碼。
    /// </summary>
    public string? CarPlateKeyword { get; set; }

    /// <summary>
    /// 目前頁碼，預設為第 1 頁。
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "頁碼至少為 1")] 
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每頁筆數，預設 20 筆以兼顧效能與資料量。
    /// </summary>
    [Range(1, 200, ErrorMessage = "每頁筆數需介於 1~200")] 
    public int PageSize { get; set; } = 20;
}
