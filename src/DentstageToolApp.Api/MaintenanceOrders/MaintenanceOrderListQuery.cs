using System;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修單列表查詢參數，支援依服務類別、狀態與建立區間進行篩選。
/// </summary>
public class MaintenanceOrderListQuery
{
    /// <summary>
    /// 維修服務類別代碼，對應資料表 Fix_Type 欄位。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 維修單狀態碼，例如 210 待確認、220 維修中、295 取消等。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 建立起始日期，採用台北時區的 DateTime 進行查詢。
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// 建立結束日期，查詢時會自動補齊至當日 23:59:59。
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// 頁碼，預設為第一頁。
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "頁碼至少為 1。")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每頁筆數，預設 20，並限制最大 200 筆避免一次撈取過多資料。
    /// </summary>
    [Range(1, 200, ErrorMessage = "每頁筆數需介於 1 到 200。")]
    public int PageSize { get; set; } = 20;
}
