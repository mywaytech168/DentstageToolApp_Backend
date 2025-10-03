using System;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價單狀態異動後回傳的共用資訊。
/// </summary>
public class QuotationStatusChangeResponse
{
    /// <summary>
    /// 估價單唯一識別碼，方便前端同步本地狀態。
    /// </summary>
    public string QuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 估價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 異動後的估價單狀態碼。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 狀態異動時間，使用台北時間回傳。
    /// </summary>
    public DateTime StatusChangedAt { get; set; }

    /// <summary>
    /// 目前預約日期（若有），方便前端更新排程資訊。
    /// </summary>
    public DateTime? ReservationDate { get; set; }
}
