using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 刪除採購品項類別時使用的請求模型，需在 Body 中提供欲刪除的類別識別碼。
/// </summary>
public class DeletePurchaseCategoryRequest
{
    /// <summary>
    /// 類別識別碼，格式為 PC_{UUID}，用於指定要刪除的資料列。
    /// </summary>
    [Required(ErrorMessage = "請提供類別識別碼。")]
    public string? CategoryUid { get; set; }
}
