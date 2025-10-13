using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Brands;

/// <summary>
/// 建立品牌的請求模型，提供品牌名稱等必填欄位。
/// </summary>
public class CreateBrandRequest
{
    /// <summary>
    /// 品牌名稱，必須輸入且限制最大長度 100。
    /// </summary>
    [Required(ErrorMessage = "請輸入品牌名稱。")]
    [MaxLength(100, ErrorMessage = "品牌名稱長度不可超過 100 個字元。")]
    public string BrandName { get; set; } = string.Empty;
}
