using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 更新採購單時的品項資料。
/// </summary>
public class UpdatePurchaseOrderItemRequest
{
    /// <summary>
    /// 採購品項識別碼，若為空表示新增品項。
    /// </summary>
    [MaxLength(100, ErrorMessage = "品項識別碼最多 100 個字元。")]
    public string? PurchaseItemUid { get; set; }

    /// <summary>
    /// 品項名稱。
    /// </summary>
    [Required(ErrorMessage = "請填寫品項名稱。")]
    [MaxLength(200, ErrorMessage = "品項名稱最多 200 個字元。")]
    public string? ItemName { get; set; }

    /// <summary>
    /// 類別識別碼。
    /// </summary>
    [MaxLength(100, ErrorMessage = "類別識別碼最多 100 個字元。")]
    public string? CategoryUid { get; set; }

    /// <summary>
    /// 單價。
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "單價不可為負數。")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 數量。
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "數量至少為 1。")]
    public int Quantity { get; set; }
}
