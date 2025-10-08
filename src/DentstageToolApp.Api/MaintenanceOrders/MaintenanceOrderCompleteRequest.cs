using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修完成時的請求資料，只需提供工單編號。
/// </summary>
public class MaintenanceOrderCompleteRequest
{
    /// <summary>
    /// 維修單編號。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;
}
