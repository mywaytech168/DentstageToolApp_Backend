using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Admin;

/// <summary>
/// 管理者查詢帳號詳情的回應資料，整合帳號基本資料與裝置清單。
/// </summary>
public class AdminAccountDetailResponse
{
    /// <summary>
    /// 使用者唯一識別碼，供前端對應帳號。
    /// </summary>
    public string UserUid { get; set; } = string.Empty;

    /// <summary>
    /// 使用者顯示名稱，同時對應店家名稱。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 使用者角色資訊，對應權限派送所需資料。
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// 帳號目前是否啟用。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 帳號最後一次登入時間，方便掌握使用情況。
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 將顯示名稱與角色整理成店家資訊，方便前端直接顯示。
    /// </summary>
    public AdminAccountStoreInfo Store { get; set; } = new();

    /// <summary>
    /// 帳號底下註冊的裝置清單。
    /// </summary>
    public IReadOnlyCollection<AdminAccountDeviceInfo> Devices { get; set; } = Array.Empty<AdminAccountDeviceInfo>();
}

/// <summary>
/// 店家資訊描述物件，將顯示名稱與角色整理為前端易讀格式。
/// </summary>
public class AdminAccountStoreInfo
{
    /// <summary>
    /// 店家名稱，取自使用者顯示名稱。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 權限角色，直接使用帳號角色欄位。
    /// </summary>
    public string? PermissionRole { get; set; }
}

/// <summary>
/// 裝置資訊描述物件，提供管理者檢視裝置註冊狀態。
/// </summary>
public class AdminAccountDeviceInfo
{
    /// <summary>
    /// 裝置註冊唯一識別碼。
    /// </summary>
    public string DeviceRegistrationUid { get; set; } = string.Empty;

    /// <summary>
    /// 裝置所屬的機碼。
    /// </summary>
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// 裝置名稱，便於辨識設備用途。
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 裝置狀態，例如 Active 或 Disabled。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 裝置是否被列為黑名單。
    /// </summary>
    public bool IsBlackListed { get; set; }

    /// <summary>
    /// 裝置註冊到期時間。
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// 裝置最後登入時間，協助判斷使用頻率。
    /// </summary>
    public DateTime? LastSignInAt { get; set; }
}
