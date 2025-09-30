namespace DentstageToolApp.Api.Auth;

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
}
