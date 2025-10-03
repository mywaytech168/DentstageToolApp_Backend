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
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修需求設定資料，提供前端回填維修選項。
    /// </summary>
    public QuotationMaintenanceInfo Maintenance { get; set; } = new();
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

