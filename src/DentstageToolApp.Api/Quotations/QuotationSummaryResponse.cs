using System;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價單摘要回應模型，提供列表頁面所需的基本資訊。
/// </summary>
public class QuotationSummaryResponse
{
    /// <summary>
    /// 估價單號，對應資料庫中的 QuotationNo 欄位。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 維修類型，協助前端根據 FixType 顯示不同標籤。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 估價單目前狀態碼，供前端依需求轉換顯示文字。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 顧客姓名，協助客服快速辨識資料。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 顧客主要聯絡電話，提供後續聯繫使用。
    /// </summary>
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// 車輛廠牌名稱。
    /// </summary>
    public string? CarBrand { get; set; }

    /// <summary>
    /// 車輛型號名稱。
    /// </summary>
    public string? CarModel { get; set; }

    /// <summary>
    /// 車牌號碼，使用已正規化的 CarNo 欄位。
    /// </summary>
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// 店鋪名稱（目前以 StoreUid 代入，待串接門市主檔後可替換為實際名稱）。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 估價技師姓名，對應資料欄位 UserName。
    /// </summary>
    public string? EstimatorName { get; set; }

    /// <summary>
    /// 製單技師或建立人員，暫以 CreatedBy 欄位呈現。
    /// </summary>
    public string? CreatorName { get; set; }

    /// <summary>
    /// 建立日期時間，使用 CreationTimestamp 欄位資訊。
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
