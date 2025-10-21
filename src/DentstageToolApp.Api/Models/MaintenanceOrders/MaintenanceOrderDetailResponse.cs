using System;
using DentstageToolApp.Api.Models.Quotations;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 維修單詳細資料回應模型，提供與估價單詳情相同的欄位結構，方便前端共用畫面元件。
/// </summary>
public class MaintenanceOrderDetailResponse : QuotationDetailResponse
{
    /// <summary>
    /// 金額資訊沿用基底類別的 <see cref="QuotationDetailResponse.Amounts"/>，確保估價與維修畫面一致。
    /// </summary>
    /// <remarks>
    /// 這裡以註解提醒開發者不要重新宣告 Amounts 屬性，以免造成序列化欄位重複。
    /// </remarks>
    // 注意：若需擴充金額欄位請調整 QuotationAmountInfo，維修單會自動跟進。

    /// <summary>
    /// 維修單唯一識別碼。
    /// </summary>
    public string OrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 維修單編號。
    /// </summary>
    public string? OrderNo { get; set; }

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
    /// 296 狀態時間戳記（維修過期）。
    /// </summary>
    public DateTime? Status296Date { get; set; }

    /// <summary>
    /// 296 狀態操作人。
    /// </summary>
    public string? Status296User { get; set; }

    /// <summary>
    /// 目前狀態異動人。
    /// </summary>
    public string? CurrentStatusUser { get; set; }
}
