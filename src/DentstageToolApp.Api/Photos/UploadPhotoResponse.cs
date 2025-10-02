namespace DentstageToolApp.Api.Photos;

/// <summary>
/// 圖片上傳成功後回傳的資訊，提供 PhotoUID 與檔案描述。
/// </summary>
public class UploadPhotoResponse
{
    /// <summary>
    /// 圖片唯一識別碼，後續估價單僅需帶入此值即可完成綁定。
    /// </summary>
    public string PhotoUid { get; set; } = string.Empty;

    /// <summary>
    /// 原始檔名，方便前端顯示或提示使用者。 
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 檔案的 Content-Type，後續下載可直接指定正確 MIME。 
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 檔案大小（位元組）。
    /// </summary>
    public long FileSize { get; set; }
}
