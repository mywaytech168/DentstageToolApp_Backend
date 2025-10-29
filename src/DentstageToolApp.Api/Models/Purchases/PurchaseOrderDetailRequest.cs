using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 查詢單筆採購單時使用的請求模型，需於 Body 內帶入採購單單號。
/// </summary>
public class PurchaseOrderDetailRequest
{
    /// <summary>
    /// 採購單單號，格式為 PO_yyyyMMxxxx，後端會依據此單號查詢對應資料。
    /// </summary>
    [Required(ErrorMessage = "請提供採購單單號。")]
    public string? PurchaseOrderNo { get; set; }
}
