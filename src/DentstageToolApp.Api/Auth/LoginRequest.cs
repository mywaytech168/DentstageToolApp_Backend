using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Auth;

/// <summary>
/// 登入請求資料傳輸物件，目前僅需攜帶裝置機碼供後端驗證。
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 裝置機碼，供後端辨識與綁定裝置使用。
    /// </summary>
    [Required(ErrorMessage = "請提供裝置機碼。")]
    [MaxLength(150, ErrorMessage = "裝置機碼長度不可超過 150 字元。")]
    public string DeviceKey { get; set; } = string.Empty;
}
