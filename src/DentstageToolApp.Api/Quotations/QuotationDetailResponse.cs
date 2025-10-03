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
    /// 傷痕細項列表，配合新版格式於頂層呈現，方便前端直接渲染表格。
    /// </summary>
    public List<QuotationDamageItem> Damages { get; set; } = new();

    /// <summary>
    /// 車體確認單資料。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修需求設定資料，提供前端回填維修選項。
    /// </summary>
    public QuotationMaintenanceInfo Maintenance { get; set; } = new();
}

