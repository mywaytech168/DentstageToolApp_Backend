using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.MaintenanceOrders;

namespace DentstageToolApp.Api.Services.MaintenanceOrder;

/// <summary>
/// 維修單服務介面，定義維修單查詢與狀態異動相關行為。
/// </summary>
public interface IMaintenanceOrderService
{
    /// <summary>
    /// 依查詢條件取得維修單列表。
    /// </summary>
    Task<MaintenanceOrderListResponse> GetOrdersAsync(MaintenanceOrderListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 取得單一維修單詳細資料。
    /// </summary>
    Task<MaintenanceOrderDetailResponse> GetOrderAsync(MaintenanceOrderDetailRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 將維修單狀態回溯至上一個有效狀態，並同步更新估價單狀態。
    /// </summary>
    Task<MaintenanceOrderStatusChangeResponse> RevertOrderAsync(MaintenanceOrderRevertRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 確認維修開始，將維修單狀態更新為 220。
    /// </summary>
    Task<MaintenanceOrderStatusChangeResponse> ConfirmMaintenanceAsync(MaintenanceOrderConfirmRequest request, string operatorName, CancellationToken cancellationToken);
}
