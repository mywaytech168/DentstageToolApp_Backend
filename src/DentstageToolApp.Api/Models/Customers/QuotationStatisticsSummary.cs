namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 按估價單維度統計的摘要資訊，提供預約、取消、總數等統計。
/// </summary>
public class QuotationStatisticsSummary
{
    /// <summary>
    /// 估價單總筆數，包含所有狀態。
    /// </summary>
    public int TotalQuotations { get; set; }

    /// <summary>
    /// 狀態屬於預約的估價單數量（110 狀態）。
    /// </summary>
    public int ReservationCount { get; set; }

    /// <summary>
    /// 狀態屬於取消或終止的估價單數量（195 狀態）。
    /// </summary>
    public int CancellationCount { get; set; }

    /// <summary>
    /// 是否有任何估價紀錄，供前端快速判斷是否需要顯示歷史。
    /// </summary>
    public bool HasQuotationHistory { get; set; }
}
