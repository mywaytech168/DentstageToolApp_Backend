using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 裝置註冊資料實體，負責記錄裝置機碼與狀態資訊。
/// </summary>
public class DeviceRegistration
{
    /// <summary>
    /// 建立時間戳記，掌握註冊建立時間。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員資訊，預設為系統帳號。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記，追蹤最後一次修改。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員資訊。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 裝置註冊唯一識別碼，作為資料表主鍵。
    /// </summary>
    public string DeviceRegistrationUid { get; set; } = null!;

    /// <summary>
    /// 所屬使用者主鍵。
    /// </summary>
    public string UserUid { get; set; } = null!;

    /// <summary>
    /// 裝置機碼，App 啟動時會上傳比對。
    /// </summary>
    public string DeviceKey { get; set; } = null!;

    /// <summary>
    /// 裝置名稱或簡短描述，方便後台辨識。
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 裝置狀態，例如 Active、Disabled。
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// 裝置是否被標記為封鎖。
    /// </summary>
    public bool IsBlackListed { get; set; }

    /// <summary>
    /// 裝置最後登入時間，方便檢查閒置狀態。
    /// </summary>
    public DateTime? LastSignInAt { get; set; }

    /// <summary>
    /// 裝置註冊過期時間，超過需要重新登入。
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// 所屬使用者導覽屬性。
    /// </summary>
    public UserAccount UserAccount { get; set; } = null!;

    /// <summary>
    /// 裝置發出的 Refresh Token 清單。
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
