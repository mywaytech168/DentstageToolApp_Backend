using System;

namespace DentstageToolApp.Api.Models.Sync;

/// <summary>
/// 定義同步相關的伺服器角色常數，並提供常用判斷工具方法。
/// </summary>
public static class SyncServerRoles
{
    /// <summary>
    /// 中央伺服器角色常數。
    /// </summary>
    public const string CentralServer = "Central";

    /// <summary>
    /// 直營門市角色常數。
    /// </summary>
    public const string DirectStore = "DirectStore";

    /// <summary>
    /// 連盟門市角色常數。
    /// </summary>
    public const string AllianceStore = "AllianceStore";

    /// <summary>
    /// 檢查指定角色是否屬於門市（直營或連盟）。
    /// </summary>
    public static bool IsStoreRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return string.Equals(role, DirectStore, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, AllianceStore, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 將角色字串正規化為定義常數，避免大小寫或別名導致判斷失敗。
    /// </summary>
    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return string.Empty;
        }

        if (string.Equals(role, CentralServer, StringComparison.OrdinalIgnoreCase))
        {
            return CentralServer;
        }

        if (string.Equals(role, DirectStore, StringComparison.OrdinalIgnoreCase))
        {
            return DirectStore;
        }

        if (string.Equals(role, AllianceStore, StringComparison.OrdinalIgnoreCase))
        {
            return AllianceStore;
        }

        return role;
    }
}
