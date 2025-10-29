using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 採購品項實體，紀錄單筆採購內容與金額。
/// </summary>
public class PurchaseItem
{
    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員名稱。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員名稱。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 採購品項唯一識別碼。
    /// </summary>
    public string PurchaseItemUid { get; set; } = null!;

    /// <summary>
    /// 所屬採購單識別碼。
    /// </summary>
    public string PurchaseOrderUid { get; set; } = null!;

    /// <summary>
    /// 品項名稱。
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// 類別識別碼，對應採購品項類別。
    /// </summary>
    public string? CategoryUid { get; set; }

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

    /// <summary>
    /// 導覽屬性：所屬採購單。
    /// </summary>
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>
    /// 導覽屬性：採購品項類別。
    /// </summary>
    public PurchaseCategory? Category { get; set; }
}
