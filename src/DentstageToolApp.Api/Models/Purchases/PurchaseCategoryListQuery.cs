using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購品項類別列表查詢參數，僅包含分頁設定，方便未來加入更多條件。
/// </summary>
public class PurchaseCategoryListQuery : PaginationRequest
{
    // 保留擴充欄位位置，例如依類別名稱篩選等後續需求。
}
