using System;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 維修單狀態異動後回傳的統一模型。
/// </summary>
public class MaintenanceOrderStatusChangeResponse
{
    /// <summary>
    /// 維修單唯一識別碼。
    /// </summary>
    public string OrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 維修單編號。
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    /// 異動後的維修單狀態碼。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 狀態異動時間。
    /// </summary>
    public DateTime? StatusTime { get; set; }

    /// <summary>
    /// 額外提示訊息。
    /// </summary>
    public string? Message { get; set; }
}
