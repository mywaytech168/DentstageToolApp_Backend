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
    Task<SyncUploadResult> ProcessUploadAsync(SyncUploadRequest request, string? remoteIpAddress, CancellationToken cancellationToken);

    /// <summary>
    /// 取得門市需要更新的差異資料。
    /// </summary>
    /// <param name="storeId">門市唯一識別碼。</param>
    /// <param name="storeType">門市型態（直營、連盟）。</param>
    /// <param name="lastSyncTime">最後一次成功同步時間。</param>
    /// <param name="pageSize">單次同步的最大筆數。</param>
    /// <param name="cancellationToken">取消作業的通知權杖。</param>
    Task<SyncDownloadResponse> GetUpdatesAsync(string storeId, string storeType, DateTime? lastSyncTime, int pageSize, string? remoteServerRole, string? remoteIpAddress, CancellationToken cancellationToken);
}
