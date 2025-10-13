using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 編輯門市的請求模型。
/// </summary>
public class EditStoreRequest
{
    /// <summary>
    /// 門市唯一識別碼。
    /// </summary>
    [Required(ErrorMessage = "請提供門市識別碼。")]
    [MaxLength(100, ErrorMessage = "門市識別碼長度不可超過 100 個字元。")]
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 門市名稱。
    /// </summary>
    [Required(ErrorMessage = "請輸入門市名稱。")]
    [MaxLength(100, ErrorMessage = "門市名稱長度不可超過 100 個字元。")]
    public string StoreName { get; set; } = string.Empty;
}
