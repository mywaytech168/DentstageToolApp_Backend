using System.Collections.Generic;

namespace DentstageToolApp.Api.CarPlates;

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
    /// 維修紀錄清單，按照時間新至舊排序。
    /// </summary>
    public IReadOnlyCollection<CarPlateMaintenanceRecordDto> Records { get; set; } = new List<CarPlateMaintenanceRecordDto>();

    /// <summary>
    /// 服務端提供的提示訊息，用於顯示在畫面上。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
