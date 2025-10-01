using System;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 建立估價單後回傳的欄位，提供前端後續導向或提示使用。
/// </summary>
public class CreateQuotationResponse
{
    /// <summary>
    /// 系統產生的估價單唯一識別碼。
    /// </summary>
    public string QuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 系統產生的估價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 建立時間，使用 UTC 儲存方便日後轉換時區。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 建立結果訊息，提供前端顯示成功提示。
    /// </summary>
    public string Message { get; set; } = "已建立估價單。";
}

