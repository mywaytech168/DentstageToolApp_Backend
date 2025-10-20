using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Technicians;

namespace DentstageToolApp.Api.Services.Technician;

/// <summary>
/// 技師維運服務介面，定義新增、更新與刪除技師資料的操作。
/// </summary>
public interface ITechnicianManagementService
{
    /// <summary>
    /// 新增技師資料。
    /// </summary>
    Task<CreateTechnicianResponse> CreateTechnicianAsync(CreateTechnicianRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新技師資料。
    /// </summary>
    Task<EditTechnicianResponse> EditTechnicianAsync(EditTechnicianRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除技師資料。
    /// </summary>
    Task<DeleteTechnicianResponse> DeleteTechnicianAsync(DeleteTechnicianRequest request, string operatorName, CancellationToken cancellationToken);
}
