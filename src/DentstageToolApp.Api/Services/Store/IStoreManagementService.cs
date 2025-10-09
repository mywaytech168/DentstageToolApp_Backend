using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Stores;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市維運服務介面，提供新增、更新與刪除門市的操作。
/// </summary>
public interface IStoreManagementService
{
    /// <summary>
    /// 建立新的門市資料。
    /// </summary>
    Task<CreateStoreResponse> CreateStoreAsync(CreateStoreRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新既有的門市資料。
    /// </summary>
    Task<EditStoreResponse> EditStoreAsync(EditStoreRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除指定的門市資料。
    /// </summary>
    Task<DeleteStoreResponse> DeleteStoreAsync(DeleteStoreRequest request, string operatorName, CancellationToken cancellationToken);
}
