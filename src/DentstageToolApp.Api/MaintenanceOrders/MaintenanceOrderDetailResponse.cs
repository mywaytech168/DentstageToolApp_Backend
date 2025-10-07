using System;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 維修單詳細資料回應模型，提供前端檢視或編輯所需資訊。
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
    /// 維修類型。
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
    /// 建立人名稱。
    /// </summary>
    public string? CreatorName { get; set; }

    /// <summary>
    /// 所屬店鋪名稱。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 估價技師名稱。
    /// </summary>
    public string? EstimatorName { get; set; }

    /// <summary>
    /// 顧客姓名。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 顧客聯絡電話。
    /// </summary>
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? CarPlate { get; set; }

    /// <summary>
    /// 車輛廠牌。
    /// </summary>
    public string? CarBrand { get; set; }

    /// <summary>
    /// 車輛型號。
    /// </summary>
    public string? CarModel { get; set; }

    /// <summary>
    /// 車色資訊。
    /// </summary>
    public string? CarColor { get; set; }

    /// <summary>
    /// 預約日期 (BookDate)。
    /// </summary>
    public string? BookDate { get; set; }

    /// <summary>
    /// 預計施工日期 (WorkDate)。
    /// </summary>
    public string? WorkDate { get; set; }

    /// <summary>
    /// 維修備註內容。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 估價金額。
    /// </summary>
    public decimal? Valuation { get; set; }

    /// <summary>
    /// 折扣金額。
    /// </summary>
    public decimal? Discount { get; set; }

    /// <summary>
    /// 實際應付金額。
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// 210 狀態時間戳記。
    /// </summary>
    public DateTime? Status210Date { get; set; }

    /// <summary>
    /// 220 狀態時間戳記。
    /// </summary>
    public DateTime? Status220Date { get; set; }

    /// <summary>
    /// 290 狀態時間戳記。
    /// </summary>
    public DateTime? Status290Date { get; set; }

    /// <summary>
    /// 295 狀態時間戳記。
    /// </summary>
    public DateTime? Status295Date { get; set; }

    /// <summary>
    /// 目前狀態異動人名稱。
    /// </summary>
    public string? CurrentStatusUser { get; set; }
}
