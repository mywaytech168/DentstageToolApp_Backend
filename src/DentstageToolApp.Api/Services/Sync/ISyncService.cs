using System;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Sync;

namespace DentstageToolApp.Api.Services.Sync;

/// <summary>
/// 定義同步資料相關的服務介面。
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// 處理門市上傳的差異資料。
    /// </summary>
    Task<SyncUploadResult> ProcessUploadAsync(SyncUploadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 取得門市需要更新的差異資料。
    /// </summary>
    Task<SyncDownloadResponse> GetUpdatesAsync(string storeId, DateTime? lastSyncTime, int pageSize, CancellationToken cancellationToken);
}
