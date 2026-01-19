using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

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
    /// 維修單狀態碼集合，例如 220 維修中、296 維修過期、295 取消等，可一次傳入多個值進行篩選。
    /// 支援 QueryString ?status=220&status=296 與 JSON ["220","296"]，後端會自動轉為 SQL IN 條件。
    /// </summary>
    public List<string>? Status { get; set; }

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

    /// <summary>
    /// 顧客姓名關鍵字，支援部分比對（例：輸入「林」可匹配「林小明」、「林先生」）。
    /// </summary>
    public string? CustomerKeyword { get; set; }

    /// <summary>
    /// 車牌關鍵字，支援部分比對（例：輸入「AAA」可匹配「AAA-123」、「12-AAA」）。
    /// 會同時比對維修單的 CarNo、CarNoInput、CarNoInputGlobal 以及關聯估價單的車牌欄位。
    /// </summary>
    public string? CarPlateKeyword { get; set; }
}
