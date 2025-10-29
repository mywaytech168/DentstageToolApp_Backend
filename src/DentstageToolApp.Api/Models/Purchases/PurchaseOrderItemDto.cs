namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單品項的資料傳輸物件。
/// </summary>
public class PurchaseOrderItemDto
{
    /// <summary>
    /// 採購品項識別碼。
    /// </summary>
    public string PurchaseItemUid { get; set; } = string.Empty;

    /// <summary>
    /// 品項名稱。
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// 類別識別碼。
    /// </summary>
    public string? CategoryUid { get; set; }

    /// <summary>
    /// 類別名稱。
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// 單價。
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 數量。
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 小計金額。
    /// </summary>
    public decimal TotalAmount { get; set; }
}
