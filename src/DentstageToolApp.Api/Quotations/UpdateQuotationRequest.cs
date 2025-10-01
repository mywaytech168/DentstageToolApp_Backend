using System.Collections.Generic;

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
}

