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

    /// <summary>
    /// 依照使用者識別碼查詢帳號資訊與裝置清單。
    /// </summary>
    Task<AdminAccountDetailResponse> GetAccountAsync(string userUid, CancellationToken cancellationToken);
}
