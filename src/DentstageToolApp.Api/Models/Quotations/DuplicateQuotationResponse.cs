using System;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 複製估價單後回傳的結果資訊。
/// </summary>
public class DuplicateQuotationResponse
{
    /// <summary>
    /// 新建立的估價單唯一識別碼。
    /// </summary>
    public string QuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 新建立的估價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 來源估價單的唯一識別碼，便於追蹤複製鏈。
    /// </summary>
    public string SourceQuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 來源估價單編號。
    /// </summary>
    public string? SourceQuotationNo { get; set; }

    /// <summary>
    /// 建立時間，使用 UTC 儲存方便日後轉換時區。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 複製結果訊息，提供前端顯示成功提示。
    /// </summary>
    public string Message { get; set; } = "已複製估價單。";
}
