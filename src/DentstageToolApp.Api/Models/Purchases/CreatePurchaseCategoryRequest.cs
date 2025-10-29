using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 建立採購品項類別的請求物件。
/// </summary>
public class CreatePurchaseCategoryRequest
{
    /// <summary>
    /// 類別名稱。
    /// </summary>
    [Required(ErrorMessage = "請填寫類別名稱。")]
    [MaxLength(100, ErrorMessage = "類別名稱最多 100 個字元。")]
    public string? CategoryName { get; set; }
}
