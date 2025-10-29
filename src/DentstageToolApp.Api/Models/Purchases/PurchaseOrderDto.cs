using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單資料傳輸物件，提供前端呈現採購主檔與品項資訊。
/// </summary>
public class PurchaseOrderDto
{
    /// <summary>
    /// 採購單識別碼。
    /// </summary>
    public string PurchaseOrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 採購單號。
    /// </summary>
    public string PurchaseOrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 採購單所屬店鋪名稱。
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
    /// 採購單品項清單。
    /// </summary>
    public IReadOnlyCollection<PurchaseOrderItemDto> Items { get; set; } = Array.Empty<PurchaseOrderItemDto>();
}
