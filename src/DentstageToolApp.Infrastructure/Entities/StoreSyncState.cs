using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 門市同步狀態實體，用來紀錄各門市最後同步時間與游標。
/// </summary>
public class StoreSyncState
{
    /// <summary>
    /// 主鍵識別碼。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 門市識別碼（直營店或連盟店編號）。
    /// </summary>
    public string StoreId { get; set; } = null!;

    /// <summary>
    /// 最後一次上傳完成時間。
    /// </summary>
    public DateTime? LastUploadTime { get; set; }

    /// <summary>
    /// 最後一次下載完成時間。
    /// </summary>
    public DateTime? LastDownloadTime { get; set; }

    /// <summary>
    /// 下載差異時使用的游標，可用於分頁控制。
    /// </summary>
    public string? LastCursor { get; set; }
}
