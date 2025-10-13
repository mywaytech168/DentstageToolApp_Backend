using System;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 續修維修單成功後回傳的估價複製結果摘要。
/// </summary>
public class MaintenanceOrderContinuationResponse
{
    /// <summary>
    /// 已取消維修單的唯一識別碼，便於前端同步狀態。
    /// </summary>
    public string CancelledOrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 已取消維修單編號。
    /// </summary>
    public string CancelledOrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 新估價單識別碼，方便前端延續顯示圖片與傷痕。
    /// </summary>
    public string QuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 新估價單編號。
    /// </summary>
    public string QuotationNo { get; set; } = string.Empty;

    /// <summary>
    /// 新估價單建立時間。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 額外提示訊息，供前端顯示友善文案。
    /// </summary>
    public string? Message { get; set; }
}
