using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models;

/// <summary>
/// 建立車型的請求模型，包含車型名稱與所屬品牌。
/// </summary>
public class CreateModelRequest
{
    /// <summary>
    /// 車型名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入車型名稱。")]
    [MaxLength(100, ErrorMessage = "車型名稱長度不可超過 100 個字元。")]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 連結的品牌識別碼，可選填供前端建立階層結構。
    /// </summary>
    [MaxLength(100, ErrorMessage = "品牌識別碼長度不可超過 100 個字元。")]
    public string? BrandUid { get; set; }
}
