using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.LicensePlates;

namespace DentstageToolApp.Api.Services.LicensePlate;

/// <summary>
/// 車牌辨識服務介面，統一定義辨識流程與回傳資料格式。
/// </summary>
public interface ILicensePlateRecognitionService
{
    /// <summary>
    /// 將上傳影像送交 Tesseract OCR 辨識，並回傳包含車輛資訊的結果。
    /// </summary>
    /// <param name="imageSource">封裝影像位元組與檔名的資料物件。</param>
    /// <param name="cancellationToken">用於取消執行的權杖，配合前端互動。</param>
    /// <returns>辨識成功時回傳結果物件，失敗時回傳 <c>null</c>。</returns>
    Task<LicensePlateRecognitionResponse?> RecognizeAsync(LicensePlateImageSource imageSource, CancellationToken cancellationToken);
}
