using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 建立採購單的請求物件。
/// </summary>
public class CreatePurchaseOrderRequest
{
    /// <summary>
    /// 建立採購單時所選擇的門市識別碼（StoreUID），即門市本身的 Token，將與門市主檔關聯。
    /// </summary>
    [Required(ErrorMessage = "請提供門市識別碼。")]
    [StringLength(100, ErrorMessage = "門市識別碼長度不可超過 100 個字元。")]
    public string? StoreUid { get; set; }

    /// <summary>
    /// 採購品項清單。
    /// </summary>
    [MinLength(1, ErrorMessage = "至少需建立一筆採購品項。")]
    [Required(ErrorMessage = "請提供採購品項。")]
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
}
