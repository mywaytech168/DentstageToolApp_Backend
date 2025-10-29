using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購品項類別列表回應模型，包含分頁資訊與類別資料集合。
/// </summary>
public class PurchaseCategoryListResponse
{
    /// <summary>
    /// 分頁資訊，提供前端掌握目前頁碼、每頁筆數與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new PaginationMetadata();

    /// <summary>
    /// 採購品項類別清單，統一以 Items 命名便於前端解析。
    /// </summary>
    public IReadOnlyCollection<PurchaseCategoryDto> Items { get; set; }
        = new List<PurchaseCategoryDto>();
}
