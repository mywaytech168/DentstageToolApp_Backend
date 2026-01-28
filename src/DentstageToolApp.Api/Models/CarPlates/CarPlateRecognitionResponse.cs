namespace DentstageToolApp.Api.Models.CarPlates;

/// <summary>
/// 車牌辨識結果資料傳輸物件，封裝辨識車牌與車輛資訊。
/// </summary>
public class CarPlateRecognitionResponse
{
    /// <summary>
    /// 辨識出的車牌號碼。
    /// </summary>
    public string? RecognitionCarPlateNumber { get; set; }

    /// <summary>
    /// 辨識出的車牌號碼。
    /// </summary>
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// Tesseract 產生的信心度百分比，範圍約為 0-100。
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 車輛識別碼。
    /// </summary>
    public string? CarUid { get; set; }

    /// <summary>
    /// 品牌識別碼。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車型識別碼。
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
    /// 車身顏色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 是否有維修紀錄。
    /// </summary>
    public bool HasMaintenanceRecords { get; set; }

    /// <summary>
    /// 是否曾經在系統中維修過。
    /// </summary>
    public bool HasMaintenanceHistory { get; set; }

    /// <summary>
    /// 里程數。
    /// </summary>
    public int? Milage { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? CarRemark { get; set; }

    /// <summary>
    /// 車牌關聯的首筆客戶資訊，優先取自最新的工單或估價單，若無客戶關聯則為 null。
    /// </summary>
    public CarPlateRelatedCustomerInfo? Customer { get; set; }

    /// <summary>
    /// 服務端提供的說明訊息，用於提示前端後續流程。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
