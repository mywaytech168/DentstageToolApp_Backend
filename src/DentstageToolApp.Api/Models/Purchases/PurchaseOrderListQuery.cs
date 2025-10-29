using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單列表查詢參數，延伸共用分頁請求並保留後續擴充空間。
/// </summary>
public class PurchaseOrderListQuery : PaginationRequest
{
    // 此處可視需求新增篩選條件，例如日期區間或類別篩選。
}
