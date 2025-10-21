using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 刪除服務類別的請求模型。
/// </summary>
public class DeleteServiceCategoryRequest
{
    /// <summary>
    /// 維修類型中文標籤。
    /// </summary>
    [Required(ErrorMessage = "請提供維修類型中文標籤。")]
    [MaxLength(50, ErrorMessage = "維修類型中文標籤長度不可超過 50 個字元。")]
    public string FixType { get; set; } = string.Empty;
}
