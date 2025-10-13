using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 建立門市的請求模型。
/// </summary>
public class CreateStoreRequest
{
    /// <summary>
    /// 門市名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入門市名稱。")]
    [MaxLength(100, ErrorMessage = "門市名稱長度不可超過 100 個字元。")]
    public string StoreName { get; set; } = string.Empty;
}
