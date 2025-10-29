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
    /// 採購單所屬門市識別碼，若傳入空白將會清除門市關聯。
    /// </summary>
    [StringLength(100, ErrorMessage = "門市識別碼長度不可超過 100 個字元。")]
    public string? StoreUid { get; set; }

    /// <summary>
    /// 採購品項清單，服務層會根據識別碼判斷新增或更新。
    /// </summary>
    [MinLength(1, ErrorMessage = "至少需保留一筆採購品項。")]
    [Required(ErrorMessage = "請提供採購品項。")]
    public List<UpdatePurchaseOrderItemRequest> Items { get; set; } = new();
}
