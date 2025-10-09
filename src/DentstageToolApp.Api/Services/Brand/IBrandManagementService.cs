using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Brands;

namespace DentstageToolApp.Api.Services.Brand;

/// <summary>
/// 品牌維運服務介面，定義新增、編輯與刪除品牌的操作。
/// </summary>
public interface IBrandManagementService
{
    /// <summary>
    /// 建立新的車輛品牌資料。
    /// </summary>
    Task<CreateBrandResponse> CreateBrandAsync(CreateBrandRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新既有的車輛品牌資料。
    /// </summary>
    Task<EditBrandResponse> EditBrandAsync(EditBrandRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除指定的車輛品牌資料。
    /// </summary>
    Task<DeleteBrandResponse> DeleteBrandAsync(DeleteBrandRequest request, string operatorName, CancellationToken cancellationToken);
}
