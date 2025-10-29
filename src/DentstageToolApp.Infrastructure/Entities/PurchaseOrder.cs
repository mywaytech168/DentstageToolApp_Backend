using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 採購單主檔實體，紀錄採購單日期與總金額等彙總資料。
/// </summary>
public class PurchaseOrder
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
    /// 採購單唯一識別碼。
    /// </summary>
    public string PurchaseOrderUid { get; set; } = null!;

    /// <summary>
    /// 採購單號，使用 PU_ 前綴搭配 GUID。
    /// </summary>
    public string PurchaseOrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 採購單所屬店鋪名稱，供前端顯示與列表模糊搜尋使用。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 採購日期。
    /// </summary>
    public DateOnly? PurchaseDate { get; set; }

    /// <summary>
    /// 採購單總金額。
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 採購單底下的品項集合。
    /// </summary>
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}
