using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 使用者帳號主檔實體，負責保存登入驗證所需的基礎資料。
/// </summary>
public class UserAccount
{
    /// <summary>
    /// 建立時間戳記，方便追蹤帳號建立時間。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員，記錄是由哪位管理者新增資料。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 最後修改時間戳記，掌握最近一次異動時間。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 最後修改人員資訊。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 使用者唯一識別碼，作為資料表主鍵。
    /// </summary>
    public string UserUid { get; set; } = null!;

    /// <summary>
    /// 顯示名稱，提供前端顯示使用。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 使用者角色資訊，決定權限等級。
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// 伺服器角色設定，用於判斷帳號屬於中央或門市環境。
    /// </summary>
    public string? ServerRole { get; set; }

    /// <summary>
    /// 伺服器對外可連線的 IP，中央伺服器將供門市同步服務使用。
    /// </summary>
    public string? ServerIp { get; set; }

    /// <summary>
    /// 帳號是否啟用中，若為 False 代表無法登入。
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 最後一次成功登入時間，用於統計或安全稽核。
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 使用者名下的裝置註冊清單。
    /// </summary>
    public ICollection<DeviceRegistration> DeviceRegistrations { get; set; } = new List<DeviceRegistration>();

    /// <summary>
    /// 使用者所擁有的 Refresh Token 清單。
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
