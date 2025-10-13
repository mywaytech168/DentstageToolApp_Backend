using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 取得維修單詳細資料的請求模型。
/// </summary>
public class MaintenanceOrderDetailRequest
{
    /// <summary>
    /// 維修單編號，作為查詢條件。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;
}
