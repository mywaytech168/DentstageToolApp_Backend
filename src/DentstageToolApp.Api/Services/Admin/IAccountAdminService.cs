using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Admin;

namespace DentstageToolApp.Api.Services.Admin;

/// <summary>
/// 定義管理者帳號維運所需的服務介面。
/// </summary>
public interface IAccountAdminService
{
    /// <summary>
    /// 建立新的使用者與對應裝置機碼。
    /// </summary>
    Task<CreateUserDeviceResponse> CreateUserWithDeviceAsync(CreateUserDeviceRequest request, CancellationToken cancellationToken);

}
