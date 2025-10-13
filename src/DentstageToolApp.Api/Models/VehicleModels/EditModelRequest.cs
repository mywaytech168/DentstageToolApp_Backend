using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.VehicleModels;

/// <summary>
/// 編輯車型的請求模型，需指定車型識別碼。
/// </summary>
public class EditModelRequest
{
    /// <summary>
    /// 車型唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供車型識別碼。")]
    [MaxLength(100, ErrorMessage = "車型識別碼長度不可超過 100 個字元。")]
    public string ModelUid { get; set; } = string.Empty;

    /// <summary>
    /// 車型名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入車型名稱。")]
    [MaxLength(100, ErrorMessage = "車型名稱長度不可超過 100 個字元。")]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 對應的品牌識別碼，可為 null 表示未指定品牌。
    /// </summary>
    [MaxLength(100, ErrorMessage = "品牌識別碼長度不可超過 100 個字元。")]
    public string? BrandUid { get; set; }
}
