using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 車牌模糊搜尋請求模型，提供車牌關鍵字查詢。
/// </summary>
public class CarPlateFuzzySearchRequest
{
    /// <summary>
    /// 車牌關鍵字，支援部分比對。
    /// </summary>
    [Required(ErrorMessage = "請輸入車牌關鍵字")]
    [StringLength(50, ErrorMessage = "車牌關鍵字長度不可超過 50 字元")]
    public string PlateKeyword { get; set; } = string.Empty;
}
