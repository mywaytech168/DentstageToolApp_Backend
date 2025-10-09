using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Cars;

/// <summary>
/// 刪除車輛資料的請求模型。
/// </summary>
public class DeleteCarRequest
{
    /// <summary>
    /// 車輛唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供車輛識別碼。")]
    [MaxLength(100, ErrorMessage = "車輛識別碼長度不可超過 100 個字元。")]
    public string CarUid { get; set; } = string.Empty;
}
