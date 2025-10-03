using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價單操作共用請求基底，提供估價單識別資訊欄位。
/// </summary>
public class QuotationActionRequestBase
{
    /// <summary>
    /// 估價單唯一識別碼，可與估價單編號擇一提供。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 估價單編號，若同時提供將優先以編號查詢。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 驗證至少需提供一種識別方式，避免後端無法定位估價單。
    /// </summary>
    public void EnsureHasIdentity()
    {
        if (string.IsNullOrWhiteSpace(QuotationUid) && string.IsNullOrWhiteSpace(QuotationNo))
        {
            throw new ValidationException("請提供估價單編號或識別碼。");
        }
    }
}
