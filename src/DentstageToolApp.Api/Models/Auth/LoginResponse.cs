namespace DentstageToolApp.Api.Models.Auth;

/// <summary>
/// 登入回應資料傳輸物件，提供 Token 與使用者資訊。
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// 存取權杖內容。
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 存取權杖到期時間（UTC）。
    /// </summary>
    public DateTime AccessTokenExpireAt { get; set; }

    /// <summary>
    /// Refresh Token 字串。
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh Token 過期時間（UTC）。
    /// </summary>
    public DateTime RefreshTokenExpireAt { get; set; }

    /// <summary>
    /// 使用者顯示名稱。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 使用者角色資訊。
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// 對裝置狀態的說明，例如 Active 或 Disabled。
    /// </summary>
    public string DeviceStatus { get; set; } = string.Empty;

    /// <summary>
    /// 服務端提示訊息，可供前端顯示或除錯。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 若為門市端登入，回傳對應的門市識別碼。
    /// </summary>
    public string? StoreId { get; set; }

    /// <summary>
    /// 若為門市端登入，回傳門市型態（Direct、Franchise 等）。
    /// </summary>
    public string? StoreType { get; set; }

    /// <summary>
    /// 對應的伺服器角色，方便前端決定同步排程或功能權限。
    /// </summary>
    public string? ServerRole { get; set; }
}
