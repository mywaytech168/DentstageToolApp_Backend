using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 刪除服務類別的請求模型。
/// </summary>
public class DeleteServiceCategoryRequest
{
    /// <summary>
    /// 服務類別唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供服務類別識別碼。")]
    [MaxLength(100, ErrorMessage = "服務類別識別碼長度不可超過 100 個字元。")]
    public string ServiceCategoryUid { get; set; } = string.Empty;
}
