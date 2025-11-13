using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 建立採購單的請求物件，品項由前端傳入，門市資訊由登入者的權杖決定。
/// </summary>
public class CreatePurchaseOrderRequest
{
    /// <summary>
    /// 採購品項清單。
    /// </summary>
    [MinLength(1, ErrorMessage = "至少需建立一筆採購品項。")]
    [Required(ErrorMessage = "請提供採購品項。")]
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();

    /// <summary>
    /// 採購日期（前端可傳入 YYYY-MM-DD），若不提供預設為建立當天。
    /// </summary>
    public DateOnly? PurchaseDate { get; set; }
}
