using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 車牌搜尋 API 的輸入模型，提供透過車牌關鍵字查詢車輛資訊的能力。
/// </summary>
public class CarPlateSearchRequest
{
    /// <summary>
    /// 欲查詢的車牌號碼，支援含連字號或空白的輸入格式。
    /// </summary>
    [Required(ErrorMessage = "請輸入欲查詢的車牌號碼。")]
    [StringLength(50, ErrorMessage = "車牌號碼長度不得超過 50 個字元。")]
    public string? CarPlate { get; set; }
}
