using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 估價單操作共用請求基底，提供估價單編號欄位。
/// </summary>
public class QuotationActionRequestBase
{
    /// <summary>
    /// 估價單編號，後端以此為唯一識別依據。
    /// </summary>
    [Required(ErrorMessage = "請提供估價單編號。")]
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 驗證估價單編號是否存在，並回傳整理後的編號字串。
    /// </summary>
    public string EnsureAndGetQuotationNo()
    {
        if (string.IsNullOrWhiteSpace(QuotationNo))
        {
            throw new ValidationException("請提供估價單編號。");
        }

        return QuotationNo.Trim();
    }
}
