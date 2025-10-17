using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Sync;

namespace DentstageToolApp.Api.Services.Sync;

/// <summary>
/// 定義呼叫中央伺服器同步 API 的標準介面。
/// </summary>
public interface IRemoteSyncApiClient
{
    /// <summary>
    /// 將門市異動資料上傳至中央伺服器。
    /// </summary>
    Task<SyncUploadResult?> UploadChangeAsync(SyncUploadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 從中央伺服器取得最新差異資料。
    /// </summary>
    Task<SyncDownloadResponse?> GetUpdatesAsync(SyncDownloadQuery query, CancellationToken cancellationToken);
}
