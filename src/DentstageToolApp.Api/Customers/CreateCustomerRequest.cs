using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Customers;

/// <summary>
/// 建立客戶資料時的輸入欄位定義，確保傳入資料符合資料庫欄位限制。
/// </summary>
public class CreateCustomerRequest
{
    /// <summary>
    /// 客戶名稱，為必填欄位。
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
    /// 客戶類別（例如一般客戶、企業客戶等）。
    /// </summary>
    [StringLength(50, ErrorMessage = "客戶類別長度不得超過 50 個字元。")]
    public string? Category { get; set; }

    /// <summary>
    /// 客戶性別，採用前端約定的值（例如 Male、Female）。
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
    /// 電子郵件，會在服務層再做進一步格式整理。
    /// </summary>
    [EmailAddress(ErrorMessage = "Email 格式不正確，請重新輸入。")]
    [StringLength(100, ErrorMessage = "Email 長度不得超過 100 個字元。")]
    public string? Email { get; set; }

    /// <summary>
    /// 消息來源，例如廣告、朋友介紹等。
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
