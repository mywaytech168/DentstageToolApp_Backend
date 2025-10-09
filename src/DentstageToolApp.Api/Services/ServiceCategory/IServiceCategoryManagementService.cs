using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.ServiceCategories;

namespace DentstageToolApp.Api.Services.ServiceCategory;

/// <summary>
/// 服務類別維運服務介面，提供建立、更新與刪除的操作。
/// </summary>
public interface IServiceCategoryManagementService
{
    /// <summary>
    /// 建立新的服務類別。
    /// </summary>
    Task<CreateServiceCategoryResponse> CreateServiceCategoryAsync(CreateServiceCategoryRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新既有的服務類別。
    /// </summary>
    Task<EditServiceCategoryResponse> EditServiceCategoryAsync(EditServiceCategoryRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除指定的服務類別。
    /// </summary>
    Task<DeleteServiceCategoryResponse> DeleteServiceCategoryAsync(DeleteServiceCategoryRequest request, string operatorName, CancellationToken cancellationToken);
}
