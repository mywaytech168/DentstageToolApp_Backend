namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價完成請求，僅需帶入估價單編號即可觸發狀態更新。
/// </summary>
public class QuotationEvaluateRequest : QuotationActionRequestBase
{
    // 目前僅沿用父類別的 QuotationNo 欄位，保留類別結構方便未來擴充其他需求。
}
