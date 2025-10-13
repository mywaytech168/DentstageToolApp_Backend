using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 估價單列表回應模型，包含分頁資訊與資料集合。
/// </summary>
public class QuotationListResponse
{
    /// <summary>
    /// 目前頁碼，回傳給前端以便同步狀態。
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每頁筆數設定值。
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 總筆數，供前端計算總頁數與顯示資訊。
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 實際載入之估價單資料列表。
    /// </summary>
    public IReadOnlyList<QuotationSummaryResponse> Items { get; set; } = new List<QuotationSummaryResponse>();
}
