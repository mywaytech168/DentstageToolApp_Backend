using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 取得估價單詳細資料時使用的請求格式，改以估價單編號為唯一查詢依據。
/// </summary>
public class GetQuotationRequest
{
    /// <summary>
    /// 估價單編號，前端僅需帶入此欄位即可取得詳細資料。
    /// </summary>
    [Required(ErrorMessage = "請提供估價單編號。")]
    public string? QuotationNo { get; set; }
}

