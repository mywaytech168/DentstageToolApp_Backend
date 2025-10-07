using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修單狀態誤按回溯的請求模型。
/// </summary>
public class MaintenanceOrderRevertRequest
{
    /// <summary>
    /// 維修單編號，供後端定位資料。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;
}
