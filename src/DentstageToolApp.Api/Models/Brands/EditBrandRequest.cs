using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Brands;

/// <summary>
/// 編輯品牌的請求模型，包含品牌識別碼與更新後的名稱。
/// </summary>
public class EditBrandRequest
{
    /// <summary>
    /// 品牌唯一識別碼，作為編輯對象。
    /// </summary>
    [Required(ErrorMessage = "請提供品牌識別碼。")]
    [MaxLength(100, ErrorMessage = "品牌識別碼長度不可超過 100 個字元。")]
    public string BrandUid { get; set; } = string.Empty;

    /// <summary>
    /// 品牌更新後的名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入品牌名稱。")]
    [MaxLength(100, ErrorMessage = "品牌名稱長度不可超過 100 個字元。")]
    public string BrandName { get; set; } = string.Empty;
}
