using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DentstageToolApp.Api.Photos;

/// <summary>
/// 上傳圖片時的輸入資料結構，支援前端以 multipart/form-data 傳遞檔案。
/// </summary>
public class UploadPhotoRequest
{
    /// <summary>
    /// 需上傳的圖片檔案，後端會產出 PhotoUID 作為後續引用識別。
    /// </summary>
    [Required(ErrorMessage = "請選擇要上傳的圖片檔案。")]
    public IFormFile? File { get; set; }

}
