using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 建立估價單時前端需提供的欄位集合，涵蓋店家、車輛、客戶與估價資訊。
/// </summary>
public class CreateQuotationRequest
{
    /// <summary>
    /// 店家相關資訊，包含估價技師 UID 與預約排程設定，對應 <see cref="CreateQuotationStoreInfo"/> 結構。
    /// </summary>
    [Required]
    public CreateQuotationStoreInfo Store { get; set; } = new();

    /// <summary>
    /// 車輛相關資訊，對應 <see cref="CreateQuotationCarInfo"/>，僅需提供車輛 UID 及可選擇覆寫品牌與車型。
    /// </summary>
    public CreateQuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 客戶相關資訊，對應 <see cref="CreateQuotationCustomerInfo"/>，主要傳遞客戶 UID 以便後端載入姓名與聯絡資料。
    /// </summary>
    public CreateQuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 維修照片依維修類型分組的集合，改以 photos 承載四種固定分類，方便前端依類別呈現。
    /// </summary>
    public QuotationPhotoRequestCollection Photos { get; set; } = new();

    /// <summary>
    /// 車體確認單資料，對應 <see cref="QuotationCarBodyConfirmation"/>，可選擇帶入受損標記、簽名影像等延伸欄位。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修需求設定，對應 <see cref="CreateQuotationMaintenanceInfo"/>，整合維修類型、估工、折扣與備註等資訊。
    /// </summary>
    public CreateQuotationMaintenanceInfo Maintenance { get; set; } = new();
}

/// <summary>
/// 建立估價單時，僅需提供操作者與排程資訊的店家欄位，其餘來源相關欄位改由後端自動帶入。
/// </summary>
public class CreateQuotationStoreInfo
{
    /// <summary>
    /// 估價技師識別碼，改為以 UID 字串傳遞，可自動帶出所屬門市與技師名稱。
    /// （後端仍保留舊欄位，待前端改版後可進一步移除。）
    /// </summary>
    [Required(ErrorMessage = "請選擇估價技師。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "請選擇有效的估價技師。")]
    public string? EstimationTechnicianUid { get; set; }

    /// <summary>
    /// 預約方式（例如電話、線上填單），提供後端統計來源使用。
    /// </summary>
    public string? BookMethod { get; set; }

    /// <summary>
    /// 預約日期，允許前端依需求傳入，若為空則代表尚未排定。
    /// </summary>
    public DateTime? ReservationDate { get; set; }

    /// <summary>
    /// 預計維修日期，允許前端依需求傳入，若為空則代表尚未排程。
    /// </summary>
    public DateTime? RepairDate { get; set; }

    /// <summary>
    /// 製單技師識別碼，若未提供則預設與估價技師相同。
    /// </summary>
    public string? CreatorTechnicianUid { get; set; }

    /// <summary>
    /// 是否為臨時客戶，True 表示本估價單為臨時客戶建立（未在客戶資料庫中正式登記）。
    /// </summary>
    public bool IsTemporaryCustomer { get; set; }
}

/// <summary>
/// 建立估價單時需指定的維修設定欄位，統一整理留車、折扣與估工等設定，由後端自行判斷維修類型。
/// </summary>
public class CreateQuotationMaintenanceInfo
{
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
    [JsonPropertyName("otherFee")]
    public decimal? OtherFee { get; set; }

    /// <summary>
    /// 預估施工花費的天數，建立時會同步寫入舊系統的 FixExpect_Day 欄位。
    /// </summary>
    public int? EstimatedRepairDays { get; set; }

    /// <summary>
    /// 預估施工花費的時數，建立時會同步寫入舊系統的 FixExpect_Hour 欄位。
    /// </summary>
    public int? EstimatedRepairHours { get; set; }

    /// <summary>
    /// 預估修復完成度（百分比），協助溝通修復後狀態。
    /// </summary>
    public decimal? EstimatedRestorationPercentage { get; set; }

    /// <summary>
    /// 修復作業預估工時（小時），對應資料表 FixTimeHour 欄位。
    /// </summary>
    public int? FixTimeHour { get; set; }

    /// <summary>
    /// 修復作業預估工時（分鐘），對應資料表 FixTimeMin 欄位。
    /// </summary>
    public int? FixTimeMin { get; set; }

    /// <summary>
    /// 預估施工完成天數，對應資料表 FixExpectDay 欄位。
    /// </summary>
    public int? FixExpectDay { get; set; }

    /// <summary>
    /// 預估施工完成小時數，對應資料表 FixExpectHour 欄位。
    /// </summary>
    public int? FixExpectHour { get; set; }

    /// <summary>
    /// 建議改採鈑烤處理的原因描述，若有內容會將 PanelBeat 欄位設為 "1"。
    /// </summary>
    public string? SuggestedPaintReason { get; set; }

    /// <summary>
    /// 無法修復時的原因說明，若有內容會將 Reject 欄位標記為 true。
    /// </summary>
    public string? UnrepairableReason { get; set; }

    /// <summary>
    /// 零頭折扣金額，協助估價單呈現整數金額。
    /// </summary>
    [JsonPropertyName("roundingDiscount")]
    public decimal? RoundingDiscount { get; set; }

    /// <summary>
    /// 折扣原因說明，利於與客戶或內部人員溝通折扣依據。
    /// </summary>
    [JsonPropertyName("discountReason")]
    public string? DiscountReason { get; set; }

    /// <summary>
    /// 依凹痕、板烤與其他三大類拆分的費用與折扣設定，對應前端面板輸入。
    /// </summary>
    public QuotationMaintenanceCategoryAdjustmentCollection? CategoryAdjustments { get; set; }

    /// <summary>
    /// 維修設定備註，取代舊版放置於頂層的 remark 欄位。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 是否已含稅，True 表示估價金額已包含稅款；False 表示估價後需另計稅款。
    /// </summary>
    public bool IncludeTax { get; set; }
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
    /// 建立估價單的使用者識別碼（UID），沿用舊欄位提供相容性。
    /// </summary>
    public string? UserUid { get; set; }

    /// <summary>
    /// 估價技師識別碼，與 <see cref="CreateQuotationStoreInfo.EstimationTechnicianUid"/> 保持一致以利串接，
    /// 並且提供前端回填使用。
    /// </summary>
    public string? EstimationTechnicianUid { get; set; }

    /// <summary>
    /// 製單技師識別碼。
    /// </summary>
    public string? CreatorTechnicianUid { get; set; }

    /// <summary>
    /// 店鋪名稱，若未提供會依據使用者或門市主檔自動補齊。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 估價技師名稱。
    /// </summary>
    public string? EstimationTechnicianName { get; set; }

    /// <summary>
    /// 製單技師名稱。
    /// </summary>
    public string? CreatorTechnicianName { get; set; }

    /// <summary>
    /// 服務技師識別碼（維修單用）。
    /// </summary>
    public string? ServiceTechnicianUid { get; set; }

    /// <summary>
    /// 服務技師名稱（維修單用）。
    /// </summary>
    public string? ServiceTechnicianName { get; set; }

    /// <summary>
    /// 建立日期。
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// 預約日期。
    /// </summary>
    public DateTime? ReservationDate { get; set; }

    /// <summary>
    /// 預約原因或特殊說明，例如客戶要求或維修要件。
    /// </summary>
    public string? ReservationContent { get; set; }

    /// <summary>
    /// 預約方式（例如電話預約、LINE 訊息）。
    /// </summary>
    public string? BookMethod { get; set; }

    /// <summary>
    /// 預約維修日期。客戶預約的實際維修日期。
    /// </summary>
    public DateTime? ReservationFixDate { get; set; }

    /// <summary>
    /// 預計維修日期。
    /// </summary>
    public DateTime? RepairDate { get; set; }

    /// <summary>
    /// 是否為臨時客戶。
    /// </summary>
    public bool? IsTemporaryCustomer { get; set; }
}

/// <summary>
/// 估價單回傳時的維修設定資訊，與建立時欄位對應。
/// </summary>
public class QuotationMaintenanceInfo
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
    /// 其他估價費用。
    /// </summary>
    [JsonPropertyName("otherFee")]
    public decimal? OtherFee { get; set; }

    /// <summary>
    /// 預估花費天數，對應資料表的 FixExpect_Day 欄位。
    /// </summary>
    public int? EstimatedRepairDays { get; set; }

    /// <summary>
    /// 預估花費時數，對應資料表的 FixExpect_Hour 欄位。
    /// </summary>
    public int? EstimatedRepairHours { get; set; }

    /// <summary>
    /// 預估修復程度（百分比）。
    /// </summary>
    public decimal? EstimatedRestorationPercentage { get; set; }

    /// <summary>
    /// 修復作業預估工時（小時）。
    /// </summary>
    public int? FixTimeHour { get; set; }

    /// <summary>
    /// 修復作業預估工時（分鐘）。
    /// </summary>
    public int? FixTimeMin { get; set; }

    /// <summary>
    /// 預估完工天數。
    /// </summary>
    public int? FixExpectDay { get; set; }

    /// <summary>
    /// 預估完工小時數。
    /// </summary>
    public int? FixExpectHour { get; set; }

    /// <summary>
    /// 建議改採鈑烤處理的原因，若有內容會將 PanelBeat 欄位設為 "1"。
    /// </summary>
    public string? SuggestedPaintReason { get; set; }

    /// <summary>
    /// 無法修復時的原因，若有內容會將 Reject 欄位標記為 true。
    /// </summary>
    public string? UnrepairableReason { get; set; }

    /// <summary>
    /// 零頭折扣金額。
    /// </summary>
    [JsonPropertyName("roundingDiscount")]
    public decimal? RoundingDiscount { get; set; }

    /// <summary>
    /// 折扣原因。
    /// </summary>
    [JsonPropertyName("discountReason")]
    public string? DiscountReason { get; set; }

    /// <summary>
    /// 依類別拆分的費用與折扣資訊，方便前端直接回填新欄位。
    /// </summary>
    public QuotationMaintenanceCategoryAdjustmentCollection? CategoryAdjustments { get; set; }

    /// <summary>
    /// 維修相關備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 是否已含稅，True 表示估價金額已包含稅款；False 表示估價後需另計稅款。
    /// </summary>
    public bool? IncludeTax { get; set; }
}

/// <summary>
/// 估價單維修設定在三大服務類別中的費用與折扣調整集合。
/// </summary>
public class QuotationMaintenanceCategoryAdjustmentCollection
{
    /// <summary>
    /// 凹痕類別的額外費用與折扣設定。
    /// </summary>
    [JsonPropertyName("dent")]
    public QuotationMaintenanceCategoryAdjustment Dent { get; set; } = new();

    /// <summary>
    /// 板烤類別的額外費用與折扣設定。
    /// </summary>
    [JsonPropertyName("paint")]
    public QuotationMaintenanceCategoryAdjustment Paint { get; set; } = new();

    /// <summary>
    /// 美容類別的額外費用與折扣設定，固定輸出欄位以支援舊資料顯示。
    /// </summary>
    [JsonPropertyName("beauty")]
    public QuotationMaintenanceCategoryAdjustment Beauty { get; set; } = new();

    /// <summary>
    /// 其他類別的額外費用與折扣設定。
    /// </summary>
    [JsonPropertyName("other")]
    public QuotationMaintenanceCategoryAdjustment Other { get; set; } = new();
}

/// <summary>
/// 維修類別中單一項目的額外費用、折扣趴數與折扣原因。
/// </summary>
public class QuotationMaintenanceCategoryAdjustment
{
    /// <summary>
    /// 類別專屬的其他費用，例如代步車或耗材。
    /// </summary>
    public decimal? OtherFee { get; set; }

    /// <summary>
    /// 類別專屬的折扣百分比，單位為百分比數值。
    /// </summary>
    public decimal? PercentageDiscount { get; set; }

    /// <summary>
    /// 類別折扣原因，利於稽核折扣依據。
    /// </summary>
    public string? DiscountReason { get; set; }
}

/// <summary>
/// 依維修類型拆分的照片集合，方便建立與編輯流程共用同一資料格式。
/// </summary>
public class QuotationPhotoRequestCollection
{
    /// <summary>
    /// 凹痕類別的維修照片列表。
    /// </summary>
    [JsonPropertyName("dent")]
    public List<QuotationDamageItem> Dent { get; set; } = new();

    /// <summary>
    /// 美容類別的維修照片列表。
    /// </summary>
    [JsonPropertyName("beauty")]
    public List<QuotationDamageItem> Beauty { get; set; } = new();

    /// <summary>
    /// 板烤類別的維修照片列表。
    /// </summary>
    [JsonPropertyName("paint")]
    public List<QuotationDamageItem> Paint { get; set; } = new();

    /// <summary>
    /// 其他類別的維修照片列表。
    /// </summary>
    [JsonPropertyName("other")]
    public List<QuotationDamageItem> Other { get; set; } = new();

    /// <summary>
    /// 將所有分類的照片集合展平成單一傷痕清單，維持舊有流程使用的資料結構。
    /// </summary>
    public List<QuotationDamageItem> ToDamageList()
    {
        var result = new List<QuotationDamageItem>();
        AppendCategory(result, Dent, "凹痕");
        AppendCategory(result, Beauty, "美容");
        AppendCategory(result, Paint, "板烤");
        AppendCategory(result, Other, "其他");
        return result;
    }

    private static void AppendCategory(List<QuotationDamageItem> target, List<QuotationDamageItem>? source, string fallbackFixType)
    {
        if (source is not { Count: > 0 })
        {
            return;
        }

        foreach (var item in source)
        {
            if (item is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.DisplayFixType))
            {
                // 若前端未指定維修類型，先以分組類別作為預設，後續仍會再以照片資料覆寫。
                item.DisplayFixType = fallbackFixType;
                item.FixTypeName = fallbackFixType;
            }

            target.Add(item);
        }
    }
}

/// <summary>
/// 建立估價單時僅需提供車輛唯一識別碼的精簡輸入結構。
/// </summary>
public class CreateQuotationCarInfo
{
    /// <summary>
    /// 車輛唯一識別碼，透過此欄位自動帶入車牌、品牌、型號、里程數等完整資訊。
    /// </summary>
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
    /// 車輛品牌識別碼。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車型名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車輛型號識別碼。
    /// </summary>
    public string? ModelUid { get; set; }

    /// <summary>
    /// 車輛里程數，回傳時以公里為單位呈現，便於維修人員掌握車況。
    /// </summary>
    public int? Mileage { get; set; }

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
    public string? CustomerUid { get; set; }
}

/// <summary>
/// 編輯估價單時僅需提供車輛唯一識別碼的精簡輸入結構。
/// 確保從前端接收的資料只含有 CarUid，其他資訊由後端從資料庫查詢補齊，
/// 避免前端直接修改車牌、品牌等基本資訊。
/// </summary>
public class UpdateQuotationCarInfo
{
    /// <summary>
    /// 車輛唯一識別碼，透過此欄位自動帶入車牌、品牌、型號、里程數等完整資訊。
    /// </summary>
    public string? CarUid { get; set; }
}

/// <summary>
/// 編輯估價單時僅需提供客戶唯一識別碼的精簡輸入結構。
/// 確保從前端接收的資料只含有 UID，其他資訊由後端從資料庫查詢補齊，
/// 避免前端直接修改客戶姓名、電話等基本資訊。
/// </summary>
public class UpdateQuotationCustomerInfo
{
    /// <summary>
    /// 客戶唯一識別碼，透過此欄位自動帶入客戶姓名與聯絡資訊。
    /// </summary>
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
    /// 電子郵件，補齊客服聯繫管道。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 顧客類型，對應資料庫 Customer_type 欄位，方便呈現客戶屬性分類。
    /// </summary>
    public string? CustomerType { get; set; }

    /// <summary>
    /// 所在縣市，對應資料庫 County 欄位。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 所在鄉鎮市區，對應資料庫 Township 欄位。
    /// </summary>
    public string? Township { get; set; }

    /// <summary>
    /// 詢問原因，對應資料庫 Reason 欄位。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 消息來源，沿用估價單 Source 欄位，優先使用門市輸入值。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 客戶備註。
    /// </summary>
    public string? Remark { get; set; }
}

