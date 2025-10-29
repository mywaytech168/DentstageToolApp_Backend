using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 查詢單筆採購單時使用的請求模型，需於 Body 內帶入採購單識別碼。
/// </summary>
public class PurchaseOrderDetailRequest
{
    /// <summary>
    /// 採購單識別碼，格式為 PU_{UUID}，用於指定要查詢的目標資料。
    /// </summary>
    [Required(ErrorMessage = "請提供採購單識別碼。")]
    public string? PurchaseOrderUid { get; set; }
}
