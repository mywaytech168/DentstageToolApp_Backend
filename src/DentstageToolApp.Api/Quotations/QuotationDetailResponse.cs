using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價單詳細資料的輸出格式，包含基本資訊與擴充欄位。
/// </summary>
public class QuotationDetailResponse
{
    /// <summary>
    /// 估價單唯一識別碼。
    /// </summary>
    public string QuotationUid { get; set; } = string.Empty;

    /// <summary>
    /// 估價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 估價單狀態。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 最後修改時間。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 店家資訊。
    /// </summary>
    public QuotationStoreInfo Store { get; set; } = new();

    /// <summary>
    /// 車輛資訊。
    /// </summary>
    public QuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 客戶資訊。
    /// </summary>
    public QuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 傷痕細項列表，採用精簡輸出格式方便前端直接使用主要欄位。
    /// </summary>
    public List<QuotationDamageSummary> Damages { get; set; } = new();

    /// <summary>
    /// 車體確認單資料。
    /// </summary>
    public QuotationCarBodyConfirmationResponse? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修需求設定資料，提供前端回填維修選項。
    /// </summary>
    public QuotationMaintenanceDetail Maintenance { get; set; } = new();
}

/// <summary>
/// 估價單傷痕的精簡輸出結構，聚焦於前端需要的核心欄位。
/// </summary>
public class QuotationDamageSummary
{
    /// <summary>
    /// 主要照片的 PhotoUID，以字串型式提供方便直接顯示。
    /// </summary>
    public string? Photos { get; set; }

    /// <summary>
    /// 車身部位或面板位置。
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// 凹痕狀態描述，例如大面積或輕微凹痕。
    /// </summary>
    public string? DentStatus { get; set; }

    /// <summary>
    /// 傷痕說明或處理建議。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 預估維修金額。
    /// </summary>
    public decimal? EstimatedAmount { get; set; }
}

/// <summary>
/// 估價單車體確認單輸出結構，僅保留損傷標記資訊供前端呈現。
/// </summary>
public class QuotationCarBodyConfirmationResponse
{
    /// <summary>
    /// 車體受損標記列表，對應前端示意圖座標與損傷狀態。
    /// </summary>
    public List<QuotationCarBodyDamageMarker> DamageMarkers { get; set; } = new();
}

/// <summary>
/// 估價單維修資訊的精簡輸出結構，移除多餘工時欄位後保留主要設定。
/// </summary>
public class QuotationMaintenanceDetail
{
    /// <summary>
    /// 維修類型識別碼，保留以供前端選單回填。
    /// </summary>
    public string? FixTypeUid { get; set; }

    /// <summary>
    /// 是否需留車。
    /// </summary>
    public bool? ReserveCar { get; set; }

    /// <summary>
    /// 是否需要鍍膜。
    /// </summary>
    public bool? ApplyCoating { get; set; }

    /// <summary>
    /// 是否需要包膜。
    /// </summary>
    public bool? ApplyWrapping { get; set; }

    /// <summary>
    /// 是否曾烤漆。
    /// </summary>
    public bool? HasRepainted { get; set; }

    /// <summary>
    /// 是否需要工具評估。
    /// </summary>
    public bool? NeedToolEvaluation { get; set; }

    /// <summary>
    /// 其他估價費用。
    /// </summary>
    public decimal? OtherFee { get; set; }

    /// <summary>
    /// 預估花費天數。
    /// </summary>
    public int? EstimatedRepairDays { get; set; }

    /// <summary>
    /// 預估花費時數。
    /// </summary>
    public int? EstimatedRepairHours { get; set; }

    /// <summary>
    /// 預估修復程度（百分比）。
    /// </summary>
    public decimal? EstimatedRestorationPercentage { get; set; }

    /// <summary>
    /// 建議改採鈑烤處理的原因。
    /// </summary>
    public string? SuggestedPaintReason { get; set; }

    /// <summary>
    /// 無法修復時的原因。
    /// </summary>
    public string? UnrepairableReason { get; set; }

    /// <summary>
    /// 零頭折扣金額。
    /// </summary>
    public decimal? RoundingDiscount { get; set; }

    /// <summary>
    /// 折扣百分比。
    /// </summary>
    public decimal? PercentageDiscount { get; set; }

    /// <summary>
    /// 折扣原因。
    /// </summary>
    public string? DiscountReason { get; set; }

    /// <summary>
    /// 維修相關備註。
    /// </summary>
    public string? Remark { get; set; }
}

