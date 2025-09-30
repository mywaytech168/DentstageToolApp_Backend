using Microsoft.AspNetCore.Http;

namespace DentstageToolApp.Api.CarPlates;

/// <summary>
/// 車牌辨識請求物件，支援表單檔案與 Base64 雙模式上傳。
/// </summary>
public class CarPlateRecognitionRequest
{
    /// <summary>
    /// 使用者上傳的車牌照片檔案，採用 multipart/form-data 格式。
    /// </summary>
    public IFormFile? Image { get; set; }

    /// <summary>
    /// 若前端無法使用檔案上傳，可改傳 Base64 影像字串。
    /// </summary>
    public string? ImageBase64 { get; set; }
}
