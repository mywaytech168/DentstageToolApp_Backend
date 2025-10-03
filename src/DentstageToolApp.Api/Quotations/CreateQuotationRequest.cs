using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 建立估價單時前端需提供的欄位集合，涵蓋店家、車輛、客戶與估價資訊。
/// </summary>
public class CreateQuotationRequest
{
    /// <summary>
    /// 店家相關資訊。
    /// </summary>
    [Required]
    public CreateQuotationStoreInfo Store { get; set; } = new();

    /// <summary>
    /// 車輛相關資訊。
    /// </summary>
    [Required]
    public CreateQuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 客戶相關資訊。
    /// </summary>
    [Required]
    public CreateQuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 傷痕細項列表，改為獨立於類別之外集中管理，便於前端統一渲染表格。
    /// </summary>
    public List<QuotationDamageItem> Damages { get; set; } = new();

    /// <summary>
    /// 車體確認單資料。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修需求設定，包含維修類型與常見的處理選項。
    /// </summary>
    public CreateQuotationMaintenanceInfo Maintenance { get; set; } = new();
}

/// <summary>
/// 建立估價單時，僅需提供操作者與來源資訊的店家欄位。
/// </summary>
public class CreateQuotationStoreInfo
{
    /// <summary>
    /// 技師識別碼，改為以 UID 字串傳遞，可自動帶出所屬門市與技師名稱。
    /// （後端仍保留舊欄位，待前端改版後可進一步移除。）
    /// </summary>
    [Required(ErrorMessage = "請選擇估價技師。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "請選擇有效的估價技師。")]
    public string? TechnicianUid { get; set; }

    /// <summary>
    /// 維修來源。
    /// </summary>
    [Required(ErrorMessage = "請輸入維修來源。")]
    public string? Source { get; set; }

    /// <summary>
    /// 預約日期，允許前端依需求傳入，若為空則代表尚未排定。
    /// </summary>
    public DateTime? ReservationDate { get; set; }

    /// <summary>
    /// 預計維修日期，允許前端依需求傳入，若為空則代表尚未排程。
    /// </summary>
    public DateTime? RepairDate { get; set; }
}

/// <summary>
/// 建立估價單時需指定的維修設定欄位，統一定義維修類型與相關選項。
/// </summary>
public class CreateQuotationMaintenanceInfo
{
    /// <summary>
    /// 維修類型識別碼（UID），用於對應維修類型主檔。
    /// </summary>
    [Required(ErrorMessage = "請選擇維修類型。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "請選擇有效的維修類型。")]
    public string? FixTypeUid { get; set; }

    /// <summary>
    /// 維修類型名稱，若前端已取得可一併傳入，後端仍會以主檔名稱為準。
    /// </summary>
    public string? FixTypeName { get; set; }

    /// <summary>
    /// 是否需留車，True 代表需要留車。
    /// </summary>
    public bool? ReserveCar { get; set; }

    /// <summary>
    /// 是否需要鍍膜服務。
    /// </summary>
    public bool? ApplyCoating { get; set; }

    /// <summary>
    /// 是否需要包膜服務。
    /// </summary>
    public bool? ApplyWrapping { get; set; }

    /// <summary>
    /// 是否曾經烤漆，供估價時參考歷史處理狀態。
    /// </summary>
    public bool? HasRepainted { get; set; }

    /// <summary>
    /// 是否需要工具評估。
    /// </summary>
    public bool? NeedToolEvaluation { get; set; }

    /// <summary>
    /// 其他估價費用，包含耗材或外包等額外支出。
    /// </summary>
    public decimal? OtherFee { get; set; }

    /// <summary>
    /// 預估施工花費的天數，提供前端呈現整體工期。
    /// </summary>
    public int? EstimatedRepairDays { get; set; }

    /// <summary>
    /// 預估施工花費的時數，可對應半天內完工等情境。
    /// </summary>
    public int? EstimatedRepairHours { get; set; }

    /// <summary>
    /// 預估修復完成度（百分比），協助溝通修復後狀態。
    /// </summary>
    public decimal? EstimatedRestorationPercentage { get; set; }

    /// <summary>
    /// 建議改採鈑烤處理的原因描述。
    /// </summary>
    public string? SuggestedPaintReason { get; set; }

    /// <summary>
    /// 無法修復時的原因說明，供前端與客戶溝通使用。
    /// </summary>
    public string? UnrepairableReason { get; set; }

    /// <summary>
    /// 維修設定備註，取代舊版放置於頂層的 remark 欄位。
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 估價單店家資訊欄位（回傳用）。
/// </summary>
public class QuotationStoreInfo
{
    /// <summary>
    /// 門市唯一代碼，可對應舊系統欄位。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 建立估價單的使用者識別碼（UID），供前端回填操作者資訊。
    /// </summary>
    public string? UserUid { get; set; }

    /// <summary>
    /// 店鋪名稱，若未提供會依據使用者或門市主檔自動補齊。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 估價人員。
    /// </summary>
    public string? EstimatorName { get; set; }

    /// <summary>
    /// 製單技師。
    /// </summary>
    public string? CreatorName { get; set; }

    /// <summary>
    /// 建立日期。
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// 預約日期。
    /// </summary>
    public DateTime? ReservationDate { get; set; }

    /// <summary>
    /// 維修來源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 預計維修日期。
    /// </summary>
    public DateTime? RepairDate { get; set; }
}

/// <summary>
/// 估價單回傳時的維修設定資訊，與建立時欄位對應。
/// </summary>
public class QuotationMaintenanceInfo
{
    /// <summary>
    /// 維修類型識別碼。
    /// </summary>
    public string? FixTypeUid { get; set; }

    /// <summary>
    /// 維修類型名稱，優先使用主檔資料。
    /// </summary>
    public string? FixTypeName { get; set; }

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
    /// 維修相關備註。
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 建立估價單時僅需提供車輛唯一識別碼的精簡輸入結構。
/// </summary>
public class CreateQuotationCarInfo
{
    /// <summary>
    /// 車輛唯一識別碼，透過此欄位自動帶入車牌、品牌等細節。
    /// </summary>
    [Required(ErrorMessage = "請選擇車輛資料。")]
    public string? CarUid { get; set; }
}

/// <summary>
/// 車輛相關資料欄位。
/// </summary>
public class QuotationCarInfo
{
    /// <summary>
    /// 車輛唯一識別碼。
    /// </summary>
    public string? CarUid { get; set; }

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? LicensePlate { get; set; }

    /// <summary>
    /// 車款或品牌名稱。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車型名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 建立估價單時僅需提供客戶唯一識別碼的精簡輸入結構。
/// </summary>
public class CreateQuotationCustomerInfo
{
    /// <summary>
    /// 客戶唯一識別碼，透過此欄位自動帶入客戶姓名與聯絡資訊。
    /// </summary>
    [Required(ErrorMessage = "請選擇客戶資料。")]
    public string? CustomerUid { get; set; }
}

/// <summary>
/// 客戶相關資料欄位（回傳用）。
/// </summary>
public class QuotationCustomerInfo
{
    /// <summary>
    /// 客戶唯一識別碼，若提供則會自動帶出客戶聯絡資料。
    /// </summary>
    [Required(ErrorMessage = "請選擇客戶資料。")]
    public string? CustomerUid { get; set; }

    /// <summary>
    /// 客戶名稱，若未提供會依據客戶主檔自動補齊。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 消息來源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 客戶備註。
    /// </summary>
    public string? Remark { get; set; }
}

