namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// APP 版本組態，透過 appsettings 維護 APK 版本資訊與檔案位置。
/// </summary>
public class AppReleaseOptions
{
    /// <summary>
    /// 目前提供給客戶端的版本名稱，例如 1.0.0。
    /// </summary>
    public string VersionName { get; set; } = string.Empty;

    /// <summary>
    /// 版本代碼，行動端可用來判斷是否需要更新。
    /// </summary>
    public int VersionCode { get; set; }

    /// <summary>
    /// 是否需要強制更新，若為 true 則行動端應阻擋舊版繼續使用。
    /// </summary>
    public bool IsForceUpdate { get; set; }

    /// <summary>
    /// 更新內容說明，顯示於前端通知使用者差異。
    /// </summary>
    public string? ChangeLog { get; set; }

    /// <summary>
    /// APK 實際檔案名稱，包含副檔名。
    /// </summary>
    public string ApkFileName { get; set; } = string.Empty;

    /// <summary>
    /// APK 儲存的實體路徑，可設定相對或絕對路徑。
    /// </summary>
    public string StorageRootPath { get; set; } = "App_Data/apk";

    /// <summary>
    /// 下載網址的根路徑，會搭配檔案名稱組合對外連結。
    /// </summary>
    public string DownloadBasePath { get; set; } = "/downloads/apk";
}
