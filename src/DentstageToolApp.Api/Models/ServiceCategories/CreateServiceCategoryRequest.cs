using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 建立服務類別（維修類型）的請求模型。
/// </summary>
public class CreateServiceCategoryRequest
{
    /// <summary>
    /// 服務類別名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入服務類別名稱。")]
    [MaxLength(100, ErrorMessage = "服務類別名稱長度不可超過 100 個字元。")]
    public string CategoryName { get; set; } = string.Empty;
}
