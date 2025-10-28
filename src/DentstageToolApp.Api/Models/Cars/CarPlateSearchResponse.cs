using System;
using System.Collections.Generic;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Models.Quotations;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 車牌搜尋 API 的回傳模型，包含車輛資訊與關聯單據列表。
/// </summary>
public class CarPlateSearchResponse
{
    /// <summary>
    /// 前端輸入的車牌關鍵字，供畫面顯示查詢條件。
    /// </summary>
    public string QueryPlate { get; set; } = string.Empty;

    /// <summary>
    /// 去除符號後的車牌字元，用於紀錄實際比對內容。
    /// </summary>
    public string QueryPlateKey { get; set; } = string.Empty;

    /// <summary>
    /// 查詢到的車輛資料，前端僅需單筆資訊，因此只保留最符合的車輛。
    /// </summary>
    public CarPlateSearchItem? Car { get; set; }

    /// <summary>
    /// 供前端提示的訊息，例如是否找到車輛或相關單據。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 車牌搜尋回傳的單筆車輛資訊。
/// </summary>
public class CarPlateSearchItem
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
    /// 車色資訊。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註說明。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 里程數，單位為公里。
    /// </summary>
    public int? Mileage { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 最後更新時間。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 相關估價單清單。
    /// </summary>
    public IReadOnlyCollection<QuotationSummaryResponse> Quotations { get; set; }
        = Array.Empty<QuotationSummaryResponse>();

    /// <summary>
    /// 相關維修單清單。
    /// </summary>
    public IReadOnlyCollection<MaintenanceOrderSummaryResponse> MaintenanceOrders { get; set; }
        = Array.Empty<MaintenanceOrderSummaryResponse>();
}
