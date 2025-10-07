using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修單確認維修的請求模型。
/// </summary>
public class MaintenanceOrderConfirmRequest
{
    /// <summary>
    /// 維修單編號，作為狀態變更依據。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;
}
