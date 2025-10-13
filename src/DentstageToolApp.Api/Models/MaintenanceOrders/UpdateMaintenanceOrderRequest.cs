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
}
