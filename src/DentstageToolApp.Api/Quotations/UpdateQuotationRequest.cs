using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 編輯估價單所需的欄位，聚焦車輛、客戶與各類別備註。
/// </summary>
public class UpdateQuotationRequest
{
    /// <summary>
    /// 估價單唯一識別碼，優先使用於更新。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 估價單編號，若未提供 UID 可改以編號更新。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 車輛資訊。
    /// </summary>
    public QuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 客戶資訊。
    /// </summary>
    public QuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 各類別整體備註，key 使用 dent、paint、other。
    /// </summary>
    public Dictionary<string, string?> CategoryRemarks { get; set; } = new();

    /// <summary>
    /// 若需要同步更新估價單整體備註可一併傳入。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 傷痕細項列表，沿用建立與詳情回傳的結構，確保欄位一致。
    /// </summary>
    [JsonConverter(typeof(QuotationDamageCollectionConverter))]
    public List<QuotationDamageItem> Damages { get; set; } = new();

    /// <summary>
    /// 車體確認單資料，包含標記與簽名資訊。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修設定資訊，與詳情回傳欄位同步，方便前端直接提交完整資料。
    /// </summary>
    public QuotationMaintenanceInfo? Maintenance { get; set; }
}

