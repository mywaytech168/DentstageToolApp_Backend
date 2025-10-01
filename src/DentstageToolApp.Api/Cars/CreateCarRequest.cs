using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Cars;

/// <summary>
/// 新增車輛資料時所需的輸入欄位定義。
/// </summary>
public class CreateCarRequest
{
    /// <summary>
    /// 車牌號碼，支援帶入破折號或空白，會在服務內統一轉成大寫後再儲存。
    /// </summary>
    [Required(ErrorMessage = "請輸入車牌號碼。")]
    [StringLength(50, ErrorMessage = "車牌號碼長度不得超過 50 個字元。")]
    public string? LicensePlateNumber { get; set; }

    /// <summary>
    /// 車輛品牌或車款資訊，可留空。
    /// </summary>
    [StringLength(50, ErrorMessage = "車輛品牌長度不得超過 50 個字元。")]
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號，可留空。
    /// </summary>
    [StringLength(50, ErrorMessage = "車輛型號長度不得超過 50 個字元。")]
    public string? Model { get; set; }

    /// <summary>
    /// 車色，可留空。
    /// </summary>
    [StringLength(50, ErrorMessage = "車色長度不得超過 50 個字元。")]
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註，紀錄維修人員補充說明。
    /// </summary>
    [StringLength(255, ErrorMessage = "備註長度不得超過 255 個字元。")]
    public string? Remark { get; set; }

    /// <summary>
    /// 操作人員名稱，若未填寫會以系統帳號代替。
    /// </summary>
    [StringLength(50, ErrorMessage = "操作人員名稱長度不得超過 50 個字元。")]
    public string? OperatorName { get; set; }
}
