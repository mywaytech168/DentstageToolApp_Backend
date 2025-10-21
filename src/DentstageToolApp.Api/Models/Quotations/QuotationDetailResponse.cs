using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DentstageToolApp.Api.Models.Quotations;

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
    /// 維修照片摘要集合，依四種維修類型分類，方便前端分群展示。
    /// </summary>
    public QuotationPhotoSummaryCollection Photos { get; set; } = new();

    /// <summary>
    /// 車體確認單資料。
    /// </summary>
    public QuotationCarBodyConfirmationResponse? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修需求設定資料，提供前端回填維修選項。
    /// </summary>
    public QuotationMaintenanceDetail Maintenance { get; set; } = new();

    /// <summary>
    /// 金額資訊物件，包含估價金額、折扣與最終應付金額，方便前端統一呈現。
    /// </summary>
    public QuotationAmountInfo Amounts { get; set; } = new();
}

/// <summary>
/// 估價單金額資訊，統一提供估價金額、折扣與應付金額欄位。
/// </summary>
public class QuotationAmountInfo
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
    /// 最終應付金額。
    /// </summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// 依維修類型分組的照片摘要集合，提供凹痕、美容、板烤與其他四種分類。
/// </summary>
public class QuotationPhotoSummaryCollection
{
    /// <summary>
    /// 凹痕類別的照片摘要列表。
    /// </summary>
    [JsonPropertyName("dent")]
    public List<QuotationDamageSummary> Dent { get; set; } = new();

    /// <summary>
    /// 美容類別的照片摘要列表。
    /// </summary>
    [JsonPropertyName("beauty")]
    public List<QuotationDamageSummary> Beauty { get; set; } = new();

    /// <summary>
    /// 板烤類別的照片摘要列表。
    /// </summary>
    [JsonPropertyName("paint")]
    public List<QuotationDamageSummary> Paint { get; set; } = new();

    /// <summary>
    /// 其他類別的照片摘要列表。
    /// </summary>
    [JsonPropertyName("other")]
    public List<QuotationDamageSummary> Other { get; set; } = new();

    /// <summary>
    /// 依加入順序列舉所有照片摘要，供後端流程維持既有計算行為。
    /// </summary>
    public IEnumerable<QuotationDamageSummary> EnumerateAll()
    {
        foreach (var item in Dent ?? new List<QuotationDamageSummary>())
        {
            if (item is not null)
            {
                yield return item;
            }
        }

        foreach (var item in Beauty ?? new List<QuotationDamageSummary>())
        {
            if (item is not null)
            {
                yield return item;
            }
        }

        foreach (var item in Paint ?? new List<QuotationDamageSummary>())
        {
            if (item is not null)
            {
                yield return item;
            }
        }

        foreach (var item in Other ?? new List<QuotationDamageSummary>())
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}

/// <summary>
/// 估價單傷痕的精簡輸出結構，聚焦於前端需要的核心欄位。
/// </summary>
public class QuotationDamageSummary
{
    private string? _fixType;
    private string? _fixTypeDisplay;

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

    /// <summary>
    /// 維修類型中文標籤，輸出時統一為凹痕、美容、板烤或其他。
    /// </summary>
    public string? FixType
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_fixTypeDisplay))
            {
                return _fixTypeDisplay;
            }

            return string.IsNullOrWhiteSpace(_fixType)
                ? null
                : QuotationDamageFixTypeHelper.ResolveDisplayName(_fixType);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _fixType = null;
                _fixTypeDisplay = null;
                return;
            }

            var resolved = QuotationDamageFixTypeHelper.ResolveDisplayName(value);
            _fixType = resolved;
            _fixTypeDisplay = resolved;
        }
    }

    /// <summary>
    /// 內部使用的維修類型顯示名稱，避免重複計算顯示文字。
    /// </summary>
    [JsonIgnore]
    public string? FixTypeName
    {
        get => _fixTypeDisplay;
        set => _fixTypeDisplay = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 舊版欄位：維修類型顯示名稱，保留 setter 供歷史資料解析。
    /// </summary>
    [JsonPropertyName("fixTypeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyFixTypeName
    {
        get => null;
        set => FixType = value;
    }
}

/// <summary>
/// 估價單車體確認單輸出結構，保留損傷標記與簽名照片 UID 供前端使用。
/// </summary>
public class QuotationCarBodyConfirmationResponse
{
    /// <summary>
    /// 車體受損標記列表，對應前端示意圖座標與損傷狀態。
    /// </summary>
    public List<QuotationCarBodyDamageMarker> DamageMarkers { get; set; } = new();

    /// <summary>
    /// 客戶簽名照片的 PhotoUID，讓前端可直接顯示簽名影像。
    /// </summary>
    public string? SignaturePhotoUid { get; set; }
}

/// <summary>
/// 估價單維修資訊的精簡輸出結構，移除多餘工時欄位後保留主要設定。
/// </summary>
public class QuotationMaintenanceDetail
{
    private string? _fixType;
    private string? _fixTypeDisplay;

    /// <summary>
    /// 維修類型中文標籤，供前端直接回填四種固定分類。
    /// </summary>
    public string? FixType
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_fixTypeDisplay))
            {
                return _fixTypeDisplay;
            }

            return string.IsNullOrWhiteSpace(_fixType)
                ? null
                : QuotationDamageFixTypeHelper.ResolveDisplayName(_fixType);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _fixType = null;
                _fixTypeDisplay = null;
                return;
            }

            var resolved = QuotationDamageFixTypeHelper.ResolveDisplayName(value);
            _fixType = resolved;
            _fixTypeDisplay = resolved;
        }
    }

    /// <summary>
    /// 維修類型顯示名稱，預設為中文描述。
    /// </summary>
    [JsonIgnore]
    public string? FixTypeName
    {
        get => _fixTypeDisplay;
        set => _fixTypeDisplay = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 舊版欄位：維修類型顯示名稱。
    /// </summary>
    [JsonPropertyName("fixTypeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyFixTypeName
    {
        get => null;
        set => FixType = value;
    }

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

