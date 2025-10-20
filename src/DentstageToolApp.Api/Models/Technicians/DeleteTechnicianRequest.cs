using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 刪除技師資料的請求模型。
/// </summary>
public class DeleteTechnicianRequest
{
    /// <summary>
    /// 技師唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供技師識別碼。")]
    [MaxLength(100, ErrorMessage = "技師識別碼長度不可超過 100 個字元。")]
    public string TechnicianUid { get; set; } = string.Empty;
}
