using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models;

namespace DentstageToolApp.Api.Services.Model;

/// <summary>
/// 車型維運服務介面，定義新增、更新與刪除車型的操作。
/// </summary>
public interface IModelManagementService
{
    /// <summary>
    /// 建立新的車型資料。
    /// </summary>
    Task<CreateModelResponse> CreateModelAsync(CreateModelRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新既有車型資料。
    /// </summary>
    Task<EditModelResponse> EditModelAsync(EditModelRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除指定車型資料。
    /// </summary>
    Task<DeleteModelResponse> DeleteModelAsync(DeleteModelRequest request, string operatorName, CancellationToken cancellationToken);
}
