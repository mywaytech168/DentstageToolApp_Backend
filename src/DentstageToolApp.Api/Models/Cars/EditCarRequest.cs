using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 編輯車輛資料時的輸入欄位定義，確保傳入內容符合資料庫限制。
/// </summary>
public class EditCarRequest
{
    /// <summary>
    /// 車輛識別碼，指定欲更新的車輛主鍵。
    /// </summary>
    [Required(ErrorMessage = "請提供車輛識別碼。")]
    [StringLength(100, ErrorMessage = "車輛識別碼長度不得超過 100 個字元。")]
    public string? CarUid { get; set; }

    /// <summary>
    /// 車牌號碼，更新時仍須符合格式檢核。
    /// </summary>
    [Required(ErrorMessage = "請輸入車牌號碼。")]
    [StringLength(50, ErrorMessage = "車牌號碼長度不得超過 50 個字元。")]
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// 車輛品牌識別碼，允許清除時傳入 null。
    /// </summary>
    [StringLength(100, ErrorMessage = "品牌識別碼長度不得超過 100 個字元。")]
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車輛型號識別碼，允許清除時傳入 null。
    /// </summary>
    [StringLength(100, ErrorMessage = "車型識別碼長度不得超過 100 個字元。")]
    public string? ModelUid { get; set; }

    /// <summary>
    /// 車色資訊。
    /// </summary>
    [StringLength(50, ErrorMessage = "車色長度不得超過 50 個字元。")]
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註，記錄維修人員補充說明。
    /// </summary>
    [StringLength(255, ErrorMessage = "備註長度不得超過 255 個字元。")]
    public string? Remark { get; set; }

    /// <summary>
    /// 車輛里程數，編輯時可一併更新，留空則不變更。
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "里程數必須為不小於 0 的整數。")]
    public int? Mileage { get; set; }
}
