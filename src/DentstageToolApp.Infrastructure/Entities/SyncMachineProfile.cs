using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 同步機碼設定，對應伺服器角色與門市資訊。
/// </summary>
public class SyncMachineProfile
{
    /// <summary>
    /// 同步機碼，對應應用程式設定的唯一識別碼。
    /// </summary>
    public string MachineKey { get; set; } = null!;

    /// <summary>
    /// 機碼對應的伺服器角色（中央、直營或連盟）。
    /// </summary>
    public string ServerRole { get; set; } = null!;

    /// <summary>
    /// 若為門市端則對應門市識別碼，中央可為空白。
    /// </summary>
    public string? StoreId { get; set; }

    /// <summary>
    /// 若為門市端則記錄門市型態（例如 Direct、Franchise）。
    /// </summary>
    public string? StoreType { get; set; }

    /// <summary>
    /// 機碼是否仍有效，預設為啟用狀態。
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 最近一次更新時間，方便追蹤設定調整時間。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 備註欄位，提供維護人員記錄用途。
    /// </summary>
    public string? Remark { get; set; }
}
