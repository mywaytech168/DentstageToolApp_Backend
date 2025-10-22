using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.CarPlates;

/// <summary>
/// 車牌維修紀錄查詢的回應資料，提供車輛與維修紀錄清單。
/// </summary>
public class CarPlateMaintenanceHistoryResponse
{
    /// <summary>
    /// 查詢後使用的正規化車牌號碼，方便前端顯示與後續查詢。
    /// </summary>
    public string CarPlateNumber { get; set; } = string.Empty;

    /// <summary>
    /// 車輛識別碼，協助前端直接導向車輛詳細資訊頁面。
    /// </summary>
    public string? CarUid { get; set; }

    /// <summary>
    /// 車輛品牌識別碼，提供前端綁定品牌選單使用。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車輛型號識別碼，對應特定品牌下的車型資料。
    /// </summary>
    public string? ModelUid { get; set; }

    /// <summary>
    /// 車輛品牌。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車輛顏色。
    /// </summary>
    public string? Color { get; set; }
    
    /// <summary>
    /// 是否擁有維修紀錄，提供前端快速判斷狀態。
    /// </summary>
    public bool HasMaintenanceRecords { get; set; }

    /// <summary>
    /// 里程
    /// </summary>
    public int? Milage { get; set; }

    /// <summary>
    /// 車輛備註
    /// </summary>
    public string? CarRemark { get; set; }

    /// <summary>
    /// 維修紀錄清單，按照時間新至舊排序。
    /// </summary>
    public IReadOnlyCollection<CarPlateMaintenanceRecordDto> Records { get; set; } = new List<CarPlateMaintenanceRecordDto>();

    /// <summary>
    /// 服務端提供的提示訊息，用於顯示在畫面上。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
