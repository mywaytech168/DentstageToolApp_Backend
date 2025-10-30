using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 照片資料實體，對應資料庫 PhotoData 資料表。
/// </summary>
public class PhotoDatum
{
    /// <summary>
    /// 照片唯一識別碼。
    /// </summary>
    public string PhotoUid { get; set; } = null!;

    /// <summary>
    /// 關聯報價單識別碼。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 關聯主體識別碼，例如工單或其他資料。
    /// </summary>
    public string? RelatedUid { get; set; }

    /// <summary>
    /// 拍攝位置。
    /// </summary>
    public string? Posion { get; set; }

    /// <summary>
    /// 文字註解。
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 傷痕形狀代碼。
    /// </summary>
    public string? PhotoShape { get; set; }

    /// <summary>
    /// 其他形狀描述。
    /// </summary>
    public string? PhotoShapeOther { get; set; }

    /// <summary>
    /// 形狀顯示文字。
    /// </summary>
    public string? PhotoShapeShow { get; set; }

    /// <summary>
    /// 預估費用。
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// 完成旗標。
    /// </summary>
    public bool? FlagFinish { get; set; }

    /// <summary>
    /// 完成費用。
    /// </summary>
    public decimal? FinishCost { get; set; }

    /// <summary>
    /// 維修進度百分比（0~100），用於計算實際收費金額。
    /// </summary>
    public decimal? MaintenanceProgress { get; set; }

    /// <summary>
    /// 照片所屬階段，例如 before、after，用於標示維修前後。
    /// </summary>
    public string? Stage { get; set; }

    /// <summary>
    /// 照片所屬維修類型，使用固定鍵值（dent、beauty、paint、other）儲存分類資訊。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 關聯報價單導航屬性。
    /// </summary>
    public Quatation? Quatation { get; set; }
}
