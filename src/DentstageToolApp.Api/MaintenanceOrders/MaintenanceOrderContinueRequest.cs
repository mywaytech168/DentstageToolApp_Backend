using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 續修維修單時的請求內容，只需傳入欲複製的工單編號。
/// </summary>
public class MaintenanceOrderContinueRequest
{
    /// <summary>
    /// 原始維修單編號。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;
}
