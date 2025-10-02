using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Photos;
using Microsoft.AspNetCore.Http;

namespace DentstageToolApp.Api.Services.Photo;

/// <summary>
/// 照片服務介面，提供上傳、讀取與綁定估價單的流程。
/// </summary>
public interface IPhotoService
{
    /// <summary>
    /// 上傳圖片並產出 PhotoUID，後續可透過此識別碼下載或綁定估價單。
    /// </summary>
    /// <param name="file">前端傳遞的圖片檔案。</param>
    /// <param name="remark">補充說明，會一併寫入照片備註欄位。</param>
    /// <param name="cancellationToken">取消權杖，供呼叫端中止上傳流程。</param>
    Task<UploadPhotoResponse> UploadAsync(IFormFile file, string? remark, CancellationToken cancellationToken);

    /// <summary>
    /// 依 PhotoUID 取得圖片檔案內容與對應的 Content-Type。
    /// </summary>
    /// <param name="photoUid">圖片唯一識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task<PhotoFile?> GetAsync(string photoUid, CancellationToken cancellationToken);

    /// <summary>
    /// 將多張圖片一次綁定至指定估價單，方便建立流程同步寫入關聯。
    /// </summary>
    /// <param name="quotationUid">估價單唯一識別碼。</param>
    /// <param name="photoUids">待綁定的圖片識別碼集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task BindToQuotationAsync(string quotationUid, IEnumerable<string> photoUids, CancellationToken cancellationToken);
}

/// <summary>
/// 圖片檔案的封裝結果，包含檔名與內容類型，方便控制器直接輸出檔案。
/// </summary>
public class PhotoFile
{
    /// <summary>
    /// 建構檔案結果，需提供檔案串流、Content-Type 與下載檔名。
    /// </summary>
    public PhotoFile(System.IO.Stream content, string contentType, string fileName)
    {
        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    /// <summary>
    /// 圖片內容串流，由呼叫端負責釋放。
    /// </summary>
    public System.IO.Stream Content { get; }

    /// <summary>
    /// 圖片的 Content-Type，預設會依副檔名推論。
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// 下載時使用的檔名，預設為原始檔名。
    /// </summary>
    public string FileName { get; }
}
