using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 建立採購單的請求物件。
/// </summary>
public class CreatePurchaseOrderRequest
{
    /// <summary>
    /// 建立採購單所屬的店鋪名稱，方便之後於列表中進行模糊搜尋。
    /// </summary>
    [StringLength(200, ErrorMessage = "店鋪名稱長度不可超過 200 個字元。")]
    public string? StoreName { get; set; }

    /// <summary>
    /// 採購品項清單。
    /// </summary>
    [MinLength(1, ErrorMessage = "至少需建立一筆採購品項。")]
    [Required(ErrorMessage = "請提供採購品項。")]
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
}
