using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 編輯技師資料的請求模型。
/// </summary>
public class EditTechnicianRequest
{
    /// <summary>
    /// 技師唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供技師識別碼。")]
    [MaxLength(100, ErrorMessage = "技師識別碼長度不可超過 100 個字元。")]
    public string TechnicianUid { get; set; } = string.Empty;

    /// <summary>
    /// 更新後的技師姓名。
    /// </summary>
    [MaxLength(100, ErrorMessage = "技師姓名長度不可超過 100 個字元。")]
    public string? TechnicianName { get; set; }

    /// <summary>
    /// 更新後的技師職稱。
    /// </summary>
    [MaxLength(50, ErrorMessage = "技師職稱長度不可超過 50 個字元。")]
    public string? JobTitle { get; set; }

    /// <summary>
    /// 更新後的所屬門市識別碼。
    /// </summary>
    [MaxLength(100, ErrorMessage = "門市識別碼長度不可超過 100 個字元。")]
    public string? StoreUid { get; set; }
}
