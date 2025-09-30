using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 車體美容設定實體，對應資料庫 CarBeautys 資料表。
/// </summary>
public class CarBeauty
{
    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 關聯報價單識別碼，亦為主鍵。
    /// </summary>
    public string QuotationUid { get; set; } = null!;

    /// <summary>
    /// 服務一主選項代碼。
    /// </summary>
    public string? Service1 { get; set; }

    /// <summary>
    /// 服務一子選項一。
    /// </summary>
    public string? Service1Sub1 { get; set; }

    /// <summary>
    /// 服務一子選項二。
    /// </summary>
    public string? Service1Sub2 { get; set; }

    /// <summary>
    /// 服務一子選項二值。
    /// </summary>
    public string? Service1Sub2Value { get; set; }

    /// <summary>
    /// 服務一顯示字串。
    /// </summary>
    public string? Service1Show { get; set; }

    /// <summary>
    /// 服務二主選項代碼。
    /// </summary>
    public string? Service2 { get; set; }

    /// <summary>
    /// 服務二值。
    /// </summary>
    public string? Service2Value { get; set; }

    /// <summary>
    /// 服務二值備註。
    /// </summary>
    public string? Service2ValueRemark { get; set; }

    /// <summary>
    /// 服務二顯示字串。
    /// </summary>
    public string? Service2Show { get; set; }

    /// <summary>
    /// 服務三主選項代碼。
    /// </summary>
    public string? Service3 { get; set; }

    /// <summary>
    /// 服務三值。
    /// </summary>
    public string? Service3Value { get; set; }

    /// <summary>
    /// 服務三顯示字串。
    /// </summary>
    public string? Service3Show { get; set; }

    /// <summary>
    /// 服務四主選項代碼。
    /// </summary>
    public string? Service4 { get; set; }

    /// <summary>
    /// 服務四第一個值。
    /// </summary>
    public string? Service4Value1 { get; set; }

    /// <summary>
    /// 服務四第一個值備註。
    /// </summary>
    public string? Service4Value1Remark { get; set; }

    /// <summary>
    /// 服務四第二個值。
    /// </summary>
    public string? Service4Value2 { get; set; }

    /// <summary>
    /// 服務四第二個值備註。
    /// </summary>
    public string? Service4Value2Remark { get; set; }

    /// <summary>
    /// 服務四顯示字串。
    /// </summary>
    public string? Service4Show { get; set; }

    /// <summary>
    /// 服務五主選項代碼。
    /// </summary>
    public string? Service5 { get; set; }

    /// <summary>
    /// 服務五值。
    /// </summary>
    public string? Service5Value { get; set; }

    /// <summary>
    /// 服務五值備註。
    /// </summary>
    public string? Service5ValueRemark { get; set; }

    /// <summary>
    /// 服務五顯示字串。
    /// </summary>
    public string? Service5Show { get; set; }

    /// <summary>
    /// 備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 服務顯示總覽。
    /// </summary>
    public string? ServiceShow { get; set; }

    /// <summary>
    /// 服務組合代碼。
    /// </summary>
    public string? ServiceCode { get; set; }

    /// <summary>
    /// 關聯報價單導航屬性。
    /// </summary>
    public Quatation? Quatation { get; set; }
}
