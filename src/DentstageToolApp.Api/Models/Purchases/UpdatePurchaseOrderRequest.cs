using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 更新採購單的請求物件。
/// </summary>
public class UpdatePurchaseOrderRequest
{
    /// <summary>
    /// 採購單識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供採購單識別碼。")]
    public string? PurchaseOrderUid { get; set; }

    /// <summary>
    /// 採購日期。
    /// </summary>
    public DateOnly? PurchaseDate { get; set; }

    /// <summary>
    /// 採購單所屬店鋪名稱，若傳入空白將會清除店鋪資訊。
    /// </summary>
    [StringLength(200, ErrorMessage = "店鋪名稱長度不可超過 200 個字元。")]
    public string? StoreName { get; set; }

    /// <summary>
    /// 採購品項清單，服務層會根據識別碼判斷新增或更新。
    /// </summary>
    [MinLength(1, ErrorMessage = "至少需保留一筆採購品項。")]
    [Required(ErrorMessage = "請提供採購品項。")]
    public List<UpdatePurchaseOrderItemRequest> Items { get; set; } = new();
}
