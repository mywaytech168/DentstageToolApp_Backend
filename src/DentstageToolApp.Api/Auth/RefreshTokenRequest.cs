using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Auth;

/// <summary>
/// 使用 Refresh Token 重新取得 Access Token 的請求物件。
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// 舊的 Refresh Token。
    /// </summary>
    [Required(ErrorMessage = "請提供 Refresh Token。")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// 再次驗證裝置機碼，確保 Token 與裝置綁定。
    /// </summary>
    [Required(ErrorMessage = "請提供裝置機碼。")]
    [MaxLength(150, ErrorMessage = "裝置機碼長度不可超過 150 字元。")]
    public string DeviceKey { get; set; } = string.Empty;
}
