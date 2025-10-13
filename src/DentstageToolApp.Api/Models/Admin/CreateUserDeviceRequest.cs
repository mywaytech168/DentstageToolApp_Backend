using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Admin;

/// <summary>
/// 管理者建立使用者與裝置時所需的請求資料，僅保留必要欄位。
/// </summary>
public class CreateUserDeviceRequest
{
    /// <summary>
    /// 使用者顯示名稱，提供前台或權杖載明使用者身分。
    /// </summary>
    [Required(ErrorMessage = "請輸入使用者顯示名稱。")]
    [MaxLength(100, ErrorMessage = "顯示名稱長度不可超過 100 字元。")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 使用者角色，預設給予一般使用者權限。
    /// </summary>
    [MaxLength(50, ErrorMessage = "角色長度不可超過 50 字元。")]
    public string? Role { get; set; }

    /// <summary>
    /// 裝置專屬機碼，登入時會以此進行驗證。
    /// </summary>
    [Required(ErrorMessage = "請提供裝置機碼。")]
    [MaxLength(150, ErrorMessage = "裝置機碼長度不可超過 150 字元。")]
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// 裝置名稱或註記，協助管理者辨識來源。
    /// </summary>
    [MaxLength(100, ErrorMessage = "裝置名稱長度不可超過 100 字元。")]
    public string? DeviceName { get; set; }

    /// <summary>
    /// 建立者名稱，便於稽核追蹤，未填寫時預設為 AdminAPI。
    /// </summary>
    [MaxLength(50, ErrorMessage = "建立者名稱長度不可超過 50 字元。")]
    public string? OperatorName { get; set; }
}
