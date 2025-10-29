namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 刪除估價單後回傳的結果資訊。
/// </summary>
public class DeleteQuotationResponse
{
    /// <summary>
    /// 已刪除的估價單識別碼。
    /// </summary>
    public string QuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 已刪除的估價單編號。
    /// </summary>
    public string QuotationNo { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息，預設提供中文提示。
    /// </summary>
    public string Message { get; set; } = "估價單已刪除。";
}
