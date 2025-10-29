using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 刪除採購單時使用的請求模型，需於 Body 帶入採購單識別碼與單號避免誤刪。
/// </summary>
public class DeletePurchaseOrderRequest
{
    /// <summary>
    /// 採購單識別碼，格式為 PU_{UUID}，用於指定要刪除的主檔資料。
    /// </summary>
    [Required(ErrorMessage = "請提供採購單識別碼。")]
    public string? PurchaseOrderUid { get; set; }

    /// <summary>
    /// 採購單單號，格式為 PO_yyyyMMxxxx，用於再次確認刪除目標避免誤刪。
    /// </summary>
    [Required(ErrorMessage = "請提供採購單單號。")]
    public string? PurchaseOrderNo { get; set; }
}
