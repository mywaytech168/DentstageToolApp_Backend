namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 取得估價單詳細資料時使用的請求格式，支援以 UID 或編號查詢。
/// </summary>
public class GetQuotationRequest
{
    /// <summary>
    /// 估價單唯一識別碼，優先使用。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 估價單編號，若未提供 UID 可改以編號查詢。
    /// </summary>
    public string? QuotationNo { get; set; }
}

