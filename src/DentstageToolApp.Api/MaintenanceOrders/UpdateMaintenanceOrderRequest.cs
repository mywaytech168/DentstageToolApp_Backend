using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DentstageToolApp.Api.Quotations;

namespace DentstageToolApp.Api.MaintenanceOrders;

/// <summary>
/// 編輯維修單時需提供的欄位，沿用估價單編輯格式以利前端重複使用表單。
/// </summary>
public class UpdateMaintenanceOrderRequest
{
    /// <summary>
    /// 維修單編號，後端依此定位工單。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 車輛資訊，沿用估價單的欄位結構。
    /// </summary>
    public QuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 客戶資訊，沿用估價單的欄位結構。
    /// </summary>
    public QuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 類別備註集合，Key 為 dent、paint、other。
    /// </summary>
    public Dictionary<string, string?> CategoryRemarks { get; set; } = new();

    /// <summary>
    /// 整體備註內容，與估價單共用欄位。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 傷痕清單，內含照片識別碼與估價金額資訊。
    /// </summary>
    [JsonConverter(typeof(QuotationDamageCollectionConverter))]
    public List<QuotationDamageItem> Damages { get; set; } = new();

    /// <summary>
    /// 車體確認單資料，包含標記與簽名資訊。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修設定資訊，包含維修類型、折扣與估工等欄位。
    /// </summary>
    public QuotationMaintenanceInfo? Maintenance { get; set; }
}
