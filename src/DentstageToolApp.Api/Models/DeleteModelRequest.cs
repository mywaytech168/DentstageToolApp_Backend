using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models;

/// <summary>
/// 刪除車型的請求模型。
/// </summary>
public class DeleteModelRequest
{
    /// <summary>
    /// 車型唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供車型識別碼。")]
    [MaxLength(100, ErrorMessage = "車型識別碼長度不可超過 100 個字元。")]
    public string ModelUid { get; set; } = string.Empty;
}
