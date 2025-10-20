using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.ServiceCategories;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Services.ServiceCategory;

/// <summary>
/// 服務類別查詢服務介面。
/// </summary>
public interface IServiceCategoryQueryService
{
    /// <summary>
    /// 取得所有服務類別列表。
    /// </summary>
    Task<ServiceCategoryListResponse> GetServiceCategoriesAsync(PaginationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 透過識別碼取得服務類別詳細資料。
    /// </summary>
    Task<ServiceCategoryDetailResponse> GetServiceCategoryAsync(string serviceCategoryUid, CancellationToken cancellationToken);
}
