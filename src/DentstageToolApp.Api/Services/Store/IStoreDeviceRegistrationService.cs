using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Stores;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市裝置註冊服務介面，負責產生註冊機碼與相關資料。
/// </summary>
public interface IStoreDeviceRegistrationService
{
    /// <summary>
    /// 建立門市裝置註冊資料並產生註冊機碼。
    /// </summary>
    Task<CreateStoreDeviceRegistrationResponse> CreateDeviceRegistrationAsync(
        CreateStoreDeviceRegistrationRequest request,
        string operatorName,
        CancellationToken cancellationToken);
}
