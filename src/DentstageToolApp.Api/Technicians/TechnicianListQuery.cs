using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Technicians;

/// <summary>
/// 技師名單查詢參數，提供前端傳遞店家識別碼。
/// </summary>
public class TechnicianListQuery
{
    /// <summary>
    /// 店家識別碼，改為以 UID 字串傳遞，長度限制 100 字元以內。
    /// </summary>
    [Required(ErrorMessage = "請提供店家識別碼。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "店家識別碼格式不正確。")]
    public string StoreUid { get; set; } = string.Empty;
}
