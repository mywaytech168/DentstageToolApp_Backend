using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.BrandModels;

namespace DentstageToolApp.Api.Services.BrandModels;

/// <summary>
/// 品牌與型號查詢服務介面，統一品牌型號資料存取入口。
/// </summary>
public interface IBrandModelQueryService
{
    /// <summary>
    /// 取得所有品牌與所屬型號清單，供前端建立品牌/車型下拉選單。
    /// </summary>
    /// <param name="cancellationToken">取消權杖，提供外部在查詢過久時取消。</param>
    /// <returns>包含品牌與型號組合的回應物件。</returns>
    Task<BrandModelListResponse> GetBrandModelsAsync(CancellationToken cancellationToken);
}
