using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.ServiceCategories;

/// <summary>
/// 編輯服務類別的請求模型。
/// </summary>
public class EditServiceCategoryRequest
{
    /// <summary>
    /// 服務類別唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供服務類別識別碼。")]
    [MaxLength(100, ErrorMessage = "服務類別識別碼長度不可超過 100 個字元。")]
    public string ServiceCategoryUid { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入服務類別名稱。")]
    [MaxLength(100, ErrorMessage = "服務類別名稱長度不可超過 100 個字元。")]
    public string CategoryName { get; set; } = string.Empty;
}
