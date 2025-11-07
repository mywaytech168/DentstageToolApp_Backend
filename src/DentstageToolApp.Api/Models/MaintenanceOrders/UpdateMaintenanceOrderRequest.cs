using System.ComponentModel.DataAnnotations;
using DentstageToolApp.Api.Models.Quotations;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 編輯維修單時需提供的欄位，沿用估價單編輯格式以利前端重複使用表單。
/// </summary>
public class UpdateMaintenanceOrderRequest : UpdateQuotationRequest
{
    /// <summary>
    /// 維修單編號，後端依此定位工單。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 可選的估價單編號（編輯維修單時非必要）。
    /// 此屬性影響 Swagger 與前端表單欄位，但會將值委派給基底的 QuotationNo，以維持既有同步邏輯。
    /// </summary>
    public new string? QuotationNo
    {
        get => base.QuotationNo;
        set => base.QuotationNo = value;
    }
}
