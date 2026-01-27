using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.CarPlates;

/// <summary>
/// 車牌維修紀錄查詢的回應資料，提供車輛與維修紀錄清單。
/// </summary>
public class CarPlateMaintenanceHistoryResponse
{
    /// <summary>
    /// 影像 / 查詢所辨識出的正規化車牌字串（例如 OCR 或前端輸入整理後的值）。
    /// 此欄位代表系統用於比對的文字，不一定等同於資料庫內儲存的正式車牌格式。
    /// </summary>
    public string? RecognitionCarPlateNumber { get; set; }

    /// <summary>
    /// 資料庫中實際對應的車牌號碼（若找到對應車輛則回傳，否則為 null）。
    /// 這個欄位可用於前端顯示與導向實際車牌／車輛詳細資訊。
    /// </summary>
    public string? CarPlateNumber { get; set; }

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
    /// 車牌關聯的首筆客戶資訊，優先取自最新的工單或估價單，若無客戶關聯則為 null。
    /// </summary>
    public CarPlateRelatedCustomerInfo? Customer { get; set; }

    /// <summary>
    /// 維修紀錄清單，按照時間新至舊排序。
    /// </summary>
    public IReadOnlyCollection<CarPlateMaintenanceRecordDto> Records { get; set; } = new List<CarPlateMaintenanceRecordDto>();

    /// <summary>
    /// 服務端提供的提示訊息，用於顯示在畫面上。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
