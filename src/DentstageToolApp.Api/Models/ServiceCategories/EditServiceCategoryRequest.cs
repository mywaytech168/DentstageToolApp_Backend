using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 編輯服務類別的請求模型。
/// </summary>
public class EditServiceCategoryRequest
{
    /// <summary>
    /// 維修類型鍵值，限定 dent、beauty、paint、other。
    /// </summary>
    [Required(ErrorMessage = "請提供維修類型鍵值。")]
    [MaxLength(50, ErrorMessage = "維修類型鍵值長度不可超過 50 個字元。")]
    public string FixType { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別顯示名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入服務類別名稱。")]
    [MaxLength(100, ErrorMessage = "服務類別名稱長度不可超過 100 個字元。")]
    public string CategoryName { get; set; } = string.Empty;
}
