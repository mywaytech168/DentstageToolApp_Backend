using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Brands;

/// <summary>
/// 刪除品牌的請求模型，僅需提供品牌識別碼。
/// </summary>
public class DeleteBrandRequest
{
    /// <summary>
    /// 品牌唯一識別碼，作為刪除目標。
    /// </summary>
    [Required(ErrorMessage = "請提供品牌識別碼。")]
    [MaxLength(100, ErrorMessage = "品牌識別碼長度不可超過 100 個字元。")]
    public string BrandUid { get; set; } = string.Empty;
}
