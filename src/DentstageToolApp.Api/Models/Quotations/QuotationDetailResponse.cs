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
    /// 最終應付金額。
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// 退傭金額，供後台統一顯示與計算淨額。
    /// </summary>
    public decimal? Rebate { get; set; }

    /// <summary>
    /// 是否開立含稅發票。
    /// </summary>
    public bool? IncludeTax { get; set; }

    /// <summary>
    /// 稅額（5%），於 IncludeTax 為 true 時自動計算。
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// 含稅後總額，計算方式：Amount + TaxAmount。
    /// </summary>
    public decimal? TotalWithTax { get; set; }
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
    private string? _photo;
    private string? _fixType;
    private string? _fixTypeDisplay;
    private string? _afterPhotoUid;

    /// <summary>
    /// 主要照片的 PhotoUID，以字串型式提供方便直接顯示。
    /// </summary>
    [JsonIgnore]
    public string? Photo
    {
        get => _photo;
        set => _photo = NormalizePhoto(value);
    }

    /// <summary>
    /// 提供對外輸出與序列化的欄位名稱，統一採用單一 photo 欄位。
    /// </summary>
    [JsonPropertyName("photo")]
    public string? DisplayPhoto
    {
        get => Photo;
        set => Photo = value;
    }

    /// <summary>
    /// 舊版欄位仍可能傳入 photos 字串，此處保留 setter 進行相容轉換。
    /// </summary>
    [JsonPropertyName("photos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPhotos
    {
        get => null;
        set => Photo = value;
    }

    /// <summary>
    /// 舊版中文欄位「圖片」，同樣匯入主要照片欄位，避免歷史資料失效。
    /// </summary>
    [JsonPropertyName("圖片")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyChinesePhoto
    {
        get => null;
        set => Photo = value;
    }

    /// <summary>
    /// 車身部位或面板位置。
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// 位置為「其他」時的補充描述。
    /// </summary>
    [JsonIgnore]
    public string? PositionOther { get; set; }

    /// <summary>
    /// 英文欄位 positionOther，提供前端使用的「其他」位置描述。
    /// </summary>
    [JsonPropertyName("positionOther")]
    public string? DisplayPositionOther
    {
        get => PositionOther;
        set => PositionOther = value;
    }

    /// <summary>
    /// 凹痕狀態描述，例如大面積或輕微凹痕。
    /// </summary>
    public string? DentStatus { get; set; }

    /// <summary>
    /// 凹痕狀態為「其他」時的補充描述。
    /// </summary>
    [JsonIgnore]
    public string? DentStatusOther { get; set; }

    /// <summary>
    /// 英文欄位 dentStatusOther，提供前端使用的「其他」凹痕狀況描述。
    /// </summary>
    [JsonPropertyName("dentStatusOther")]
    public string? DisplayDentStatusOther
    {
        get => DentStatusOther;
        set => DentStatusOther = value;
    }

    /// <summary>
    /// 傷痕說明或處理建議。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 預估維修金額。
    /// </summary>
    public decimal? EstimatedAmount { get; set; }

    /// <summary>
    /// 拆裝費用。
    /// </summary>
    public decimal? DismantlingFee { get; set; }

    /// <summary>
    /// 維修類型中文標籤，僅供後端內部判斷分群與歷史資料相容，不再直接輸出至 API。
    /// </summary>
    [JsonIgnore]
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
    /// 舊版欄位：維修類型代碼，保留 setter 供歷史資料解析。
    /// </summary>
    /// <remarks>
    /// 這裡仍接收舊欄位寫入，並在序列化時回傳 null，確保回應不再出現 fixType 屬性。
    /// </remarks>
    [JsonPropertyName("fixType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyFixType
    {
        get => null;
        set => FixType = value;
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

    /// <summary>
    /// 維修進度百分比（0~100），提供詳情畫面顯示進度。
    /// </summary>
    [JsonIgnore]
    public decimal? MaintenanceProgress { get; set; }

    /// <summary>
    /// 新欄位：MaintenanceProgress，提供前端顯示與提交維修進度。
    /// </summary>
    [JsonPropertyName("maintenanceProgress")]
    public decimal? DisplayMaintenanceProgress
    {
        get => MaintenanceProgress;
        set => MaintenanceProgress = value;
    }

    /// <summary>
    /// 舊欄位：progressPercentage，保留 setter 以相容舊版資料。
    /// </summary>
    [JsonPropertyName("progressPercentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LegacyProgressPercentage
    {
        get => null;
        set => MaintenanceProgress = value;
    }

    /// <summary>
    /// 實際收費金額，依據進度與預估金額計算。
    /// </summary>
    public decimal? ActualAmount { get; set; }

    /// <summary>
    /// 維修後照片識別碼，指向對應的完工照片。
    /// </summary>
    [JsonIgnore]
    public string? AfterPhotoUid
    {
        get => _afterPhotoUid;
        set => _afterPhotoUid = NormalizePhoto(value);
    }

    /// <summary>
    /// 將 afterPhotoUid 以固定欄位名稱輸出，維持前後端欄位一致。
    /// </summary>
    [JsonPropertyName("afterPhotoUid")]
    public string? DisplayAfterPhotoUid
    {
        get => AfterPhotoUid;
        set => AfterPhotoUid = value;
    }

    /// <summary>
    /// 舊版欄位：afterPhotos 陣列，保留 setter 將第一筆資料轉換為 afterPhotoUid。
    /// </summary>
    [JsonPropertyName("afterPhotos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LegacyAfterPhotos
    {
        get => null;
        set
        {
            if (value is { Count: > 0 })
            {
                AfterPhotoUid = value[0];
            }
        }
    }

    /// <summary>
    /// 將輸入的照片識別碼進行正規化，避免空白或空字串造成判斷落差。
    /// </summary>
    private static string? NormalizePhoto(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
    /// 是否含稅。
    /// </summary>
    public bool? IncludeTax { get; set; }

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
    /// 各類別拆分的其他費用與折扣設定，提供凹痕、板烤與其他三大類資料。
    /// </summary>
    public QuotationMaintenanceCategoryAdjustmentCollection CategoryAdjustments { get; set; } = new();

    /// <summary>
    /// 維修相關備註。
    /// </summary>
    public string? Remark { get; set; }
}

