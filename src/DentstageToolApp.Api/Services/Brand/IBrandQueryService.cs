using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Brands;

namespace DentstageToolApp.Api.Services.Brand;

/// <summary>
/// 品牌查詢服務介面，提供列表與明細資料取得。
/// </summary>
public interface IBrandQueryService
{
    /// <summary>
    /// 取得品牌列表。
    /// </summary>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>品牌列表回應。</returns>
    Task<BrandListResponse> GetBrandsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 透過識別碼取得品牌詳細資料。
    /// </summary>
    /// <param name="brandUid">品牌識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>品牌詳細資料。</returns>
    Task<BrandDetailResponse> GetBrandAsync(string brandUid, CancellationToken cancellationToken);
}
