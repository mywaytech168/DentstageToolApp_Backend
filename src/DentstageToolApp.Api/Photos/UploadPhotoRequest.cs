using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DentstageToolApp.Api.Photos;

/// <summary>
/// 上傳圖片時的輸入資料結構，支援前端以 multipart/form-data 傳遞檔案與備註。
/// </summary>
public class UploadPhotoRequest
{
    /// <summary>
    /// 需上傳的圖片檔案，後端會產出 PhotoUID 作為後續引用識別。
    /// </summary>
    [Required(ErrorMessage = "請選擇要上傳的圖片檔案。")]
    public IFormFile? File { get; set; }

    /// <summary>
    /// 圖片備註說明，可用於記錄拍攝角度或用途。
    /// </summary>
    [MaxLength(500, ErrorMessage = "備註長度不可超過 500 個字元。")]
    public string? Remark { get; set; }
}
