using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Auth;

/// <summary>
/// 登入請求資料傳輸物件，負責攜帶帳號密碼與裝置信息。
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 登入帳號。
    /// </summary>
    [Required(ErrorMessage = "帳號為必填欄位。")]
    [MaxLength(100, ErrorMessage = "帳號長度不可超過 100 字元。")]
    public string Account { get; set; } = string.Empty;

    /// <summary>
    /// 登入密碼（明碼，服務端會負責雜湊比對）。
    /// </summary>
    [Required(ErrorMessage = "密碼為必填欄位。")]
    [MaxLength(100, ErrorMessage = "密碼長度不可超過 100 字元。")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 裝置機碼，供後端綁定裝置使用。
    /// </summary>
    [Required(ErrorMessage = "請提供裝置機碼。")]
    [MaxLength(150, ErrorMessage = "裝置機碼長度不可超過 150 字元。")]
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// 裝置顯示名稱，方便後台辨識來源。
    /// </summary>
    [MaxLength(100, ErrorMessage = "裝置名稱長度不可超過 100 字元。")]
    public string? DeviceName { get; set; }
}
