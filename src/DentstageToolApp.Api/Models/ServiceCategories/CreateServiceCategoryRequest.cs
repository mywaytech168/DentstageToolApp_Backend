using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 建立服務類別（維修類型）的請求模型。
/// </summary>
public class CreateServiceCategoryRequest
{
    /// <summary>
    /// 維修類型中文標籤，限定凹痕、美容、板烤或其他四種固定分類。
    /// </summary>
    [Required(ErrorMessage = "請輸入維修類型中文標籤。")]
    [MaxLength(50, ErrorMessage = "維修類型中文標籤長度不可超過 50 個字元。")]
    public string FixType { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別顯示名稱，例如凹痕、美容等中文說明。
    /// </summary>
    [Required(ErrorMessage = "請輸入服務類別名稱。")]
    [MaxLength(100, ErrorMessage = "服務類別名稱長度不可超過 100 個字元。")]
    public string CategoryName { get; set; } = string.Empty;
}
