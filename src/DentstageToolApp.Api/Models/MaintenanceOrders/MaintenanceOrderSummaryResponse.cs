using System;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 維修單列表單筆資料的回應結構，提供列表畫面所需欄位。
/// </summary>
public class MaintenanceOrderSummaryResponse
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
    /// 維修單目前狀態碼。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 顧客姓名。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 車輛廠牌。
    /// </summary>
    public string? CarBrand { get; set; }

    /// <summary>
    /// 車輛型號。
    /// </summary>
    public string? CarModel { get; set; }

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? CarPlate { get; set; }

    /// <summary>
    /// 估價技師識別碼。
    /// </summary>
    public string? EstimatorUid { get; set; }

    /// <summary>
    /// 製單技師識別碼。
    /// </summary>
    public string? CreatorUid { get; set; }

    /// <summary>
    /// 所屬店鋪名稱。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 估價技師名稱。
    /// </summary>
    public string? EstimatorName { get; set; }

    /// <summary>
    /// 製單技師或建立人員名稱。
    /// </summary>
    public string? CreatorName { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
