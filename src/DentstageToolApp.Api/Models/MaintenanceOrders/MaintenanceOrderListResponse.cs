using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 維修單列表回應模型，包含分頁資訊與資料集合。
/// </summary>
public class MaintenanceOrderListResponse
{
    /// <summary>
    /// 目前頁碼。
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每頁筆數。
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 總筆數。
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 實際載入之維修單清單。
    /// </summary>
    public IReadOnlyCollection<MaintenanceOrderSummaryResponse> Items { get; set; }
        = new List<MaintenanceOrderSummaryResponse>();
}
