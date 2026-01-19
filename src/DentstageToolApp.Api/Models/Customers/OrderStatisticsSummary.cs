namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 按維修單維度統計的摘要資訊，提供預約、取消、總數等統計。
/// </summary>
public class OrderStatisticsSummary
{
    /// <summary>
    /// 維修單總筆數，包含所有狀態。
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// 狀態屬於預約（已開工）的維修單數量（220 狀態）。
    /// </summary>
    public int ReservationCount { get; set; }

    /// <summary>
    /// 狀態屬於取消或終止的維修單數量（290、295 狀態）。
    /// </summary>
    public int CancellationCount { get; set; }

    /// <summary>
    /// 是否有任何維修紀錄，供前端快速判斷是否需要顯示歷史。
    /// </summary>
    public bool HasOrderHistory { get; set; }
}
