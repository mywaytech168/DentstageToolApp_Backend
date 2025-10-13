using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 隨機估價單測試頁面回傳結構，提供前端快速帶入建立估價單的預設資料。
/// </summary>
public class CreateQuotationTestPageResponse
{
    /// <summary>
    /// 隨機產生的估價單建立請求內容，前端可直接帶入現有新增 API 測試流程。
    /// </summary>
    public CreateQuotationRequest Draft { get; set; } = new();

    /// <summary>
    /// 使用到的技師摘要資訊，方便前端顯示目前測試資料對應的人員。
    /// </summary>
    public CreateQuotationTestEntitySummary? Technician { get; set; }

    /// <summary>
    /// 使用到的門市摘要資訊。
    /// </summary>
    public CreateQuotationTestEntitySummary? Store { get; set; }

    /// <summary>
    /// 使用到的客戶摘要資訊。
    /// </summary>
    public CreateQuotationTestEntitySummary? Customer { get; set; }

    /// <summary>
    /// 使用到的車輛摘要資訊。
    /// </summary>
    public CreateQuotationTestEntitySummary? Car { get; set; }

    /// <summary>
    /// 使用到的維修類型摘要資訊。
    /// </summary>
    public CreateQuotationTestEntitySummary? FixType { get; set; }

    /// <summary>
    /// 指示此次資料是否實際取用資料庫既有資料，或完全由系統隨機生成。
    /// </summary>
    public bool UsedExistingData { get; set; }

    /// <summary>
    /// 產生時間，方便測試人員確認資料的新鮮度。
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// 系統產生資料時的注意事項說明，提供前端顯示於測試頁面。
    /// </summary>
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// 測試頁面用的資料摘要結構，以最小欄位呈現測試對象資訊。
/// </summary>
public class CreateQuotationTestEntitySummary
{
    /// <summary>
    /// 實體唯一識別碼。
    /// </summary>
    public string? Uid { get; set; }

    /// <summary>
    /// 實體名稱或顯示文字。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 額外描述資訊，例如車牌、電話或門市名稱。
    /// </summary>
    public string? Description { get; set; }
}
