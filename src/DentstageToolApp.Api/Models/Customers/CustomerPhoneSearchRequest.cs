using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 電話搜尋 API 的輸入模型，提供查詢指定電話號碼的能力。
/// </summary>
public class CustomerPhoneSearchRequest
{
    /// <summary>
    /// 欲查詢的電話號碼，支援包含符號的輸入，後端會自動去除空白。
    /// </summary>
    [Required(ErrorMessage = "請輸入欲查詢的電話號碼。")]
    [StringLength(50, ErrorMessage = "電話號碼長度不得超過 50 個字元。")]
    public string? Phone { get; set; }
}
