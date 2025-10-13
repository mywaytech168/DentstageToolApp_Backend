using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 刪除客戶資料的請求模型。
/// </summary>
public class DeleteCustomerRequest
{
    /// <summary>
    /// 客戶唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供客戶識別碼。")]
    [MaxLength(100, ErrorMessage = "客戶識別碼長度不可超過 100 個字元。")]
    public string CustomerUid { get; set; } = string.Empty;
}
