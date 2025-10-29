using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單列表回應物件。
/// </summary>
public class PurchaseOrderListResponse
{
    /// <summary>
    /// 分頁資訊，讓前端得知目前頁碼與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new PaginationMetadata();

    /// <summary>
    /// 採購單列表資料集合，統一以 Items 命名提供前端解析。
    /// </summary>
    public IReadOnlyCollection<PurchaseOrderDto> Items { get; set; } = new List<PurchaseOrderDto>();
}
