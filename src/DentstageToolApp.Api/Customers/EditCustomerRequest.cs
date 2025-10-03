using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Customers;

/// <summary>
/// 編輯客戶資料時的輸入欄位定義。
/// </summary>
public class EditCustomerRequest
{
    /// <summary>
    /// 客戶識別碼，指定欲更新的客戶主鍵。
    /// </summary>
    [Required(ErrorMessage = "請提供客戶識別碼。")]
    [StringLength(100, ErrorMessage = "客戶識別碼長度不得超過 100 個字元。")]
    public string? CustomerUid { get; set; }

    /// <summary>
    /// 客戶名稱，更新時仍為必填欄位。
    /// </summary>
    [Required(ErrorMessage = "請輸入客戶名稱。")]
    [StringLength(100, ErrorMessage = "客戶名稱長度不得超過 100 個字元。")]
    public string? CustomerName { get; set; }

    /// <summary>
    /// 聯絡電話，可留空但若有填寫需符合長度限制。
    /// </summary>
    [StringLength(20, ErrorMessage = "聯絡電話長度不得超過 20 個字元。")]
    public string? Phone { get; set; }

    /// <summary>
    /// 客戶類別。
    /// </summary>
    [StringLength(50, ErrorMessage = "客戶類別長度不得超過 50 個字元。")]
    public string? Category { get; set; }

    /// <summary>
    /// 客戶性別。
    /// </summary>
    [StringLength(10, ErrorMessage = "性別長度不得超過 10 個字元。")]
    public string? Gender { get; set; }

    /// <summary>
    /// 所在縣市資訊。
    /// </summary>
    [StringLength(50, ErrorMessage = "縣市長度不得超過 50 個字元。")]
    public string? County { get; set; }

    /// <summary>
    /// 所在區域或鄉鎮。
    /// </summary>
    [StringLength(50, ErrorMessage = "區域長度不得超過 50 個字元。")]
    public string? Township { get; set; }

    /// <summary>
    /// 電子郵件。
    /// </summary>
    [EmailAddress(ErrorMessage = "Email 格式不正確，請重新輸入。")]
    [StringLength(100, ErrorMessage = "Email 長度不得超過 100 個字元。")]
    public string? Email { get; set; }

    /// <summary>
    /// 消息來源。
    /// </summary>
    [StringLength(100, ErrorMessage = "消息來源長度不得超過 100 個字元。")]
    public string? Source { get; set; }

    /// <summary>
    /// 為何選擇本服務的原因或需求描述。
    /// </summary>
    [StringLength(255, ErrorMessage = "為何選擇長度不得超過 255 個字元。")]
    public string? Reason { get; set; }

    /// <summary>
    /// 其他備註資訊。
    /// </summary>
    [StringLength(255, ErrorMessage = "備註長度不得超過 255 個字元。")]
    public string? Remark { get; set; }
}
