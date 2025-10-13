namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// JWT 設定值，從組態載入並提供服務使用。
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Token 發行者。
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Token 受眾。
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// 簽章用密鑰字串。
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Access Token 有效分鐘數。
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>
    /// Refresh Token 有效天數。
    /// </summary>
    public int RefreshTokenDays { get; set; } = 30;
}
