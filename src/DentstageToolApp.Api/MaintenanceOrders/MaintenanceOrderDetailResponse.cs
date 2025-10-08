using System;
using System.Collections.Generic;
using DentstageToolApp.Api.Quotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修單詳細資料回應模型，提供與估價單詳情相同的欄位結構，方便前端共用畫面元件。
/// </summary>
public class MaintenanceOrderDetailResponse
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
    /// 關聯估價單唯一識別碼。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 關聯估價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 維修單狀態碼。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 維修類型名稱，沿用維修單資料表內容供前端呈現文字。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 最後異動時間。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 店鋪與建單人資訊，沿用估價單詳情的巢狀結構。
    /// </summary>
    public QuotationStoreInfo Store { get; set; } = new();

    /// <summary>
    /// 車輛資訊，與估價單詳情保持一致欄位方便前端回填。
    /// </summary>
    public QuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 顧客資訊，沿用估價單詳情結構。
    /// </summary>
    public QuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 傷痕列表，使用估價單的精簡輸出格式以便直接顯示主要欄位。
    /// </summary>
    public List<QuotationDamageSummary> Damages { get; set; } = new();

    /// <summary>
    /// 車體確認單資料，若維修單缺少相對應資訊則可為 null。
    /// </summary>
    public QuotationCarBodyConfirmationResponse? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修設定資訊，沿用估價單維修欄位，並同步帶入維修單的備註與折扣。
    /// </summary>
    public QuotationMaintenanceDetail Maintenance { get; set; } = new();

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
