using DentstageToolApp.Api.Models.Sync;

namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// 同步機制的組態選項，提供伺服器角色與排程設定。
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// 伺服器角色，分為中央伺服器、直營門市或連盟門市。
    /// </summary>
    public string? ServerRole { get; set; }

    /// <summary>
    /// 門市識別碼，僅在直營或連盟門市角色時需要設定。
    /// </summary>
    public string? StoreId { get; set; }

    /// <summary>
    /// 門市型態（例如 Direct、Franchise），供呼叫同步 API 時使用。
    /// </summary>
    public string? StoreType { get; set; }

    /// <summary>
    /// 背景同步的排程間隔（分鐘），預設為 60 分鐘執行一次。
    /// </summary>
    public int BackgroundSyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 將伺服器角色轉換為既定常數，避免大小寫造成判斷差異。
    /// </summary>
    public string NormalizedServerRole => SyncServerRoles.Normalize(ServerRole);

    /// <summary>
    /// 判斷目前設定是否為門市角色（直營或連盟）。
    /// </summary>
    public bool IsStoreRole => SyncServerRoles.IsStoreRole(ServerRole);
}
