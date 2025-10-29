using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 採購品項類別實體，記錄採購品項的分類資訊供前端維護。
/// </summary>
public class PurchaseCategory
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
    /// 類別唯一識別碼。
    /// </summary>
    public string CategoryUid { get; set; } = null!;

    /// <summary>
    /// 類別顯示名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// 類別底下的採購品項集合。
    /// </summary>
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}
