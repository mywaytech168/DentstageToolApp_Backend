using System;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 續修維修單成功後回傳的新工單摘要資訊。
/// </summary>
public class MaintenanceOrderContinuationResponse
{
    /// <summary>
    /// 新建維修單唯一識別碼。
    /// </summary>
    public string OrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 新建維修單編號。
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 關聯估價單識別碼，方便前端延續顯示圖片與傷痕。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 關聯估價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 維修單建立時間。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 新建維修單的初始狀態碼。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 額外提示訊息，供前端顯示友善文案。
    /// </summary>
    public string? Message { get; set; }
}
