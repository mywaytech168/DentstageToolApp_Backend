using System;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 車輛詳細資料回應物件，提供車牌、品牌與備註等資訊。
/// </summary>
public class CarDetailResponse
{
    /// <summary>
    /// 車輛識別碼。
    /// </summary>
    public string CarUid { get; set; } = string.Empty;

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// 車輛品牌名稱。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 備註說明。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 最後修改時間戳記。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 建立人員。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 最後修改人員。
    /// </summary>
    public string? ModifiedBy { get; set; }
}
