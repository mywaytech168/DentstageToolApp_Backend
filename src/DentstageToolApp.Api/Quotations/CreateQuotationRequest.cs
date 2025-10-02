using System;
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
    /// 服務類別的明細資訊。
    /// </summary>
    public QuotationServiceCategoryCollection? ServiceCategories { get; set; }

    /// <summary>
    /// 各類別金額總覽。
    /// </summary>
    public QuotationCategoryTotal? CategoryTotal { get; set; }

    /// <summary>
    /// 車體確認單資料。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 估價單整體備註，會以 JSON 包裝儲存在資料庫中。
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 建立估價單時，僅需提供技師與來源資訊的店家欄位。
/// </summary>
public class CreateQuotationStoreInfo
{
    /// <summary>
    /// 技師識別碼，僅需提供此欄位即可自動帶出所屬門市與技師名稱。
    /// </summary>
    [Required(ErrorMessage = "請選擇估價技師。")]
    [Range(1, int.MaxValue, ErrorMessage = "請選擇有效的估價技師。")]
    public int? TechnicianId { get; set; }

    /// <summary>
    /// 維修來源。
    /// </summary>
    [Required(ErrorMessage = "請輸入維修來源。")]
    public string? Source { get; set; }
}

/// <summary>
/// 估價單店家資訊欄位（回傳用）。
/// </summary>
public class QuotationStoreInfo
{
    /// <summary>
    /// 門市識別碼，若有對應主檔可一併傳入。
    /// </summary>
    public int? StoreId { get; set; }

    /// <summary>
    /// 門市唯一代碼，可對應舊系統欄位。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 技師識別碼，僅需提供此欄位即可自動帶出所屬門市與技師名稱。
    /// </summary>
    public int? TechnicianId { get; set; }

    /// <summary>
    /// 店鋪名稱，若未提供會依據技師或門市主檔自動補齊。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 估價技師。
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

