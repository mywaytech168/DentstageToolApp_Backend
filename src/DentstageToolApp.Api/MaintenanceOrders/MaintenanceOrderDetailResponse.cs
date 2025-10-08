using System;
using DentstageToolApp.Api.Quotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修單詳細資料回應模型，提供與估價單詳情相同的欄位結構，方便前端共用畫面元件。
/// </summary>
public class MaintenanceOrderDetailResponse : QuotationDetailResponse
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
    /// 維修單金額資訊，保留估價金額、折扣與實際金額供前端呈現。
    /// </summary>
    public MaintenanceOrderAmountInfo Amounts { get; set; } = new();

    /// <summary>
    /// 維修狀態歷程，提供各狀態時間與操作人。
    /// </summary>
    public MaintenanceOrderStatusHistory StatusHistory { get; set; } = new();

    /// <summary>
    /// 目前狀態異動人名稱，保留頂層欄位以維持舊有相容性。
    /// </summary>
    public string? CurrentStatusUser { get; set; }
}

/// <summary>
/// 維修單金額資訊，補齊估價金額、折扣與應付金額。
/// </summary>
public class MaintenanceOrderAmountInfo
{
    /// <summary>
    /// 估價金額。
    /// </summary>
    public decimal? Valuation { get; set; }

    /// <summary>
    /// 折扣金額。
    /// </summary>
    public decimal? Discount { get; set; }

    /// <summary>
    /// 折扣百分比。
    /// </summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>
    /// 實際應付金額。
    /// </summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// 維修單各狀態節點的時間與操作資訊。
/// </summary>
public class MaintenanceOrderStatusHistory
{
    /// <summary>
    /// 210 狀態時間戳記。
    /// </summary>
    public DateTime? Status210Date { get; set; }

    /// <summary>
    /// 210 狀態操作人。
    /// </summary>
    public string? Status210User { get; set; }

    /// <summary>
    /// 220 狀態時間戳記。
    /// </summary>
    public DateTime? Status220Date { get; set; }

    /// <summary>
    /// 220 狀態操作人。
    /// </summary>
    public string? Status220User { get; set; }

    /// <summary>
    /// 290 狀態時間戳記。
    /// </summary>
    public DateTime? Status290Date { get; set; }

    /// <summary>
    /// 290 狀態操作人。
    /// </summary>
    public string? Status290User { get; set; }

    /// <summary>
    /// 295 狀態時間戳記。
    /// </summary>
    public DateTime? Status295Date { get; set; }

    /// <summary>
    /// 295 狀態操作人。
    /// </summary>
    public string? Status295User { get; set; }

    /// <summary>
    /// 目前狀態異動人。
    /// </summary>
    public string? CurrentStatusUser { get; set; }
}
