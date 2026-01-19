using System;
using System.Collections.Generic;
using DentstageToolApp.Api.Models.MaintenanceOrders;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 車牌模糊搜尋回應模型，回傳符合條件的車輛清單。
/// </summary>
public class CarPlateFuzzySearchResponse
{
    /// <summary>
    /// 前端輸入的車牌關鍵字。
    /// </summary>
    public string QueryKeyword { get; set; } = string.Empty;

    /// <summary>
    /// 符合條件的車輛清單。
    /// </summary>
    public IReadOnlyCollection<CarPlateFuzzySearchItem> Cars { get; set; }
        = Array.Empty<CarPlateFuzzySearchItem>();

    /// <summary>
    /// 查詢結果總筆數。
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 是否有更多結果（保留欄位，固定為 false）。
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// 提示訊息。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 車牌模糊搜尋回傳的單筆車輛資訊。
/// </summary>
public class CarPlateFuzzySearchItem
{
    /// <summary>
    /// 車輛唯一識別碼。
    /// </summary>
    public string CarUid { get; set; } = string.Empty;

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// 車輛品牌名稱。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 品牌型號組合字串。
    /// </summary>
    public string? BrandModel { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 里程數（公里）。
    /// </summary>
    public int? Mileage { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? CarRemark { get; set; }

    /// <summary>
    /// 相關估價單數量。
    /// </summary>
    public int QuotationCount { get; set; }

    /// <summary>
    /// 相關維修單數量。
    /// </summary>
    public int MaintenanceOrderCount { get; set; }

    /// <summary>
    /// 維修單摘要清單。
    /// </summary>
    public List<MaintenanceOrderSummaryResponse> MaintenanceOrders { get; set; } = new();
}
