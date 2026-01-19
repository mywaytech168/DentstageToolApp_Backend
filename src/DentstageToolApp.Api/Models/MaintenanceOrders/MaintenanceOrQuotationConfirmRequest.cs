namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 用於估價端或維修端觸發確認維修的請求：
/// - 傳入 QuotationNo 則代表由估價單觸發「建立維修單並進入維修中」流程。
/// - 傳入 OrderNo 則代表由維修單編號確認既有工單（相容舊流程）。
/// </summary>
public class MaintenanceOrQuotationConfirmRequest
{
    /// <summary>
    /// 可選的估價單編號（若要由估價單建立維修單，請填此欄位）。
    /// </summary>
    public string? QuotationNo { get; set; }
}
