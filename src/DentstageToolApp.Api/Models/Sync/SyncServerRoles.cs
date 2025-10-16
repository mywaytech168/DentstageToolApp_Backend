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
    public const string CentralServer = "中央";

    /// <summary>
    /// 直營門市角色常數。
    /// </summary>
    public const string DirectStore = "直營店";

    /// <summary>
    /// 加盟門市角色常數。
    /// </summary>
    public const string AllianceStore = "加盟店";

    /// <summary>
    /// 檢查指定角色是否為同步流程支援的角色（中央或任一門市）。
    /// </summary>
    public static bool IsStoreRole(string? role)
    {
        var normalized = Normalize(role);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return string.Equals(normalized, CentralServer, StringComparison.Ordinal)
            || string.Equals(normalized, DirectStore, StringComparison.Ordinal)
            || string.Equals(normalized, AllianceStore, StringComparison.Ordinal);
    }

    /// <summary>
    /// 檢查指定角色是否為門市角色（直營或連盟）。
    /// </summary>
    public static bool IsBranchRole(string? role)
    {
        var normalized = Normalize(role);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return string.Equals(normalized, DirectStore, StringComparison.Ordinal)
            || string.Equals(normalized, AllianceStore, StringComparison.Ordinal);
    }

    /// <summary>
    /// 檢查指定角色是否為中央伺服器角色。
    /// </summary>
    public static bool IsCentralRole(string? role)
    {
        var normalized = Normalize(role);
        return !string.IsNullOrWhiteSpace(normalized)
            && string.Equals(normalized, CentralServer, StringComparison.Ordinal);
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

        if (string.Equals(role, CentralServer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Central", StringComparison.OrdinalIgnoreCase))
        {
            return CentralServer;
        }

        if (string.Equals(role, DirectStore, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "DirectStore", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Direct", StringComparison.OrdinalIgnoreCase))
        {
            return DirectStore;
        }

        if (string.Equals(role, AllianceStore, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "AllianceStore", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Alliance", StringComparison.OrdinalIgnoreCase))
        {
            return AllianceStore;
        }

        return role;
    }
}
