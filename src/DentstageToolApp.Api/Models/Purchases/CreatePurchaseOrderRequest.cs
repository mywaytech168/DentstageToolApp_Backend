using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 建立採購單的請求物件。
/// </summary>
public class CreatePurchaseOrderRequest
{
    /// <summary>
    /// 採購日期。
    /// </summary>
    [Required(ErrorMessage = "請填寫採購日期。")]
    public DateOnly? PurchaseDate { get; set; }

    /// <summary>
    /// 採購品項清單。
    /// </summary>
    [MinLength(1, ErrorMessage = "至少需建立一筆採購品項。")]
    [Required(ErrorMessage = "請提供採購品項。")]
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
}
