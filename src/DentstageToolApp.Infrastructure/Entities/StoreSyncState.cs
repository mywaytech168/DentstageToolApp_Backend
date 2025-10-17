using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 門市同步狀態實體，用來紀錄各門市最後同步時間與同步統計資訊。
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
    /// 門市型態（Direct、Franchise 等），用來區分同步流程歸屬。
    /// </summary>
    public string StoreType { get; set; } = null!;

    /// <summary>
    /// 伺服器角色（中央或門市），方便中央端快速辨識來源屬性。
    /// </summary>
    public string? ServerRole { get; set; }

    /// <summary>
    /// 最近一次通訊時紀錄的伺服器 IP，利於排查網路連線。
    /// </summary>
    public string? ServerIp { get; set; }

    /// <summary>
    /// 最後一次上傳完成時間。
    /// </summary>
    public DateTime? LastUploadTime { get; set; }

    /// <summary>
    /// 最後一次下載完成時間。
    /// </summary>
    public DateTime? LastDownloadTime { get; set; }

    /// <summary>
    /// 最近一次同步時實際取得的資料筆數，利於監控同步效率。
    /// </summary>
    public int LastSyncCount { get; set; }
}
