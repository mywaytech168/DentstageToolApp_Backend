using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// Refresh Token 實體，負責記錄長期登入授權資料。
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員或系統來源。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員資訊。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Refresh Token 主鍵。
    /// </summary>
    public string RefreshTokenUid { get; set; } = null!;

    /// <summary>
    /// Token 字串內容。
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// Token 所屬使用者主鍵。
    /// </summary>
    public string UserUid { get; set; } = null!;

    /// <summary>
    /// 產生 Token 的裝置註冊主鍵，可為空代表系統操作。
    /// </summary>
    public string? DeviceRegistrationUid { get; set; }

    /// <summary>
    /// Token 過期時間。
    /// </summary>
    public DateTime ExpireAt { get; set; }

    /// <summary>
    /// 最後使用時間，用於分析使用情況。
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 是否已被撤銷，撤銷後不可再使用。
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 撤銷時間，配合 IsRevoked 使用。
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 導覽屬性：Token 所屬使用者。
    /// </summary>
    public UserAccount UserAccount { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：產生 Token 的裝置註冊資料。
    /// </summary>
    public DeviceRegistration? DeviceRegistration { get; set; }
}
