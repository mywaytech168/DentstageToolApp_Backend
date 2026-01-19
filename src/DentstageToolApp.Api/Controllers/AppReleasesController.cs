using System;
using DentstageToolApp.Api.Models.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// APP 版本控制器，提供查詢最新 APK 版本資訊與下載的端點。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AppReleasesController : ControllerBase
{
    private readonly AppReleaseOptions _appReleaseOptions;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AppReleasesController> _logger;

    /// <summary>
    /// 建構式注入版本組態與環境資訊，方便統一由 appsettings 管理版本資訊。
    /// </summary>
    /// <param name="appReleaseOptions">APP 版本設定資料。</param>
    /// <param name="env">主機環境資訊。</param>
    /// <param name="logger">日誌記錄器。</param>
    public AppReleasesController(
        IOptions<AppReleaseOptions> appReleaseOptions,
        IWebHostEnvironment env,
        ILogger<AppReleasesController> logger)
    {
        // 直接取出組態內容供後續使用，若缺少值則於啟動時即會拋例外
        _appReleaseOptions = appReleaseOptions.Value;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// 查詢目前最新版本資訊，並回傳是否需要更新與下載網址。
    /// </summary>
    /// <param name="versionCode">客戶端目前安裝的版本代碼。</param>
    /// <returns>最新版本資訊以及強制更新判斷結果。</returns>
    [HttpGet("latest")]
    public IActionResult GetLatest([FromQuery] int? versionCode = null)
    {
        // 安全處理查詢參數，若未帶入則視為 0 代表尚未安裝
        var clientVersionCode = versionCode ?? 0;
        // 只要客戶端版本代碼小於最新版本即判定需要更新
        var shouldUpdate = clientVersionCode < _appReleaseOptions.VersionCode;

        var response = new
        {
            versionName = _appReleaseOptions.VersionName,
            versionCode = _appReleaseOptions.VersionCode,
            changeLog = _appReleaseOptions.ChangeLog ?? string.Empty,
            downloadUrl = BuildDownloadUrl(),
            shouldUpdate
        };

        return Ok(response);
    }

    /// <summary>
    /// 下載最新版本 APK 檔案。
    /// </summary>
    /// <returns>APK 檔案串流。</returns>
    [HttpGet("download")]
    public IActionResult DownloadApk()
    {
        var apkPath = Path.IsPathRooted(_appReleaseOptions.StorageRootPath)
            ? Path.Combine(_appReleaseOptions.StorageRootPath, _appReleaseOptions.ApkFileName)
            : Path.Combine(_env.ContentRootPath, _appReleaseOptions.StorageRootPath, _appReleaseOptions.ApkFileName);

        if (!System.IO.File.Exists(apkPath))
        {
            _logger.LogWarning("APK 檔案不存在：{ApkPath}", apkPath);
            return NotFound(new { message = "APK 檔案不存在" });
        }

        try
        {
            var fileBytes = System.IO.File.ReadAllBytes(apkPath);
            var fileName = _appReleaseOptions.ApkFileName;

            _logger.LogInformation("提供 APK 下載：{FileName}，大小：{Size} bytes", fileName, fileBytes.Length);

            return File(fileBytes, "application/vnd.android.package-archive", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "讀取 APK 檔案時發生錯誤：{ApkPath}", apkPath);
            return StatusCode(500, new { message = "讀取檔案時發生錯誤" });
        }
    }

    /// <summary>
    /// 下載指定版本的 APK 檔案。
    /// </summary>
    /// <param name="version">版本名稱，例如：1.0.1</param>
    /// <returns>APK 檔案串流。</returns>
    [HttpGet("download/{version}")]
    public IActionResult DownloadSpecificVersion(string version)
    {
        var fileName = $"dentstage-tool-app-v{version}";
        /*
        var apkPath = Path.IsPathRooted(_appReleaseOptions.StorageRootPath)
            ? Path.Combine(_appReleaseOptions.StorageRootPath, fileName)
            : Path.Combine(_env.ContentRootPath, _appReleaseOptions.StorageRootPath, fileName);
        */

        var apkPath = Path.IsPathRooted(_appReleaseOptions.StorageRootPath)
            ? Path.Combine(_appReleaseOptions.StorageRootPath, _appReleaseOptions.ApkFileName)
            : Path.Combine(_env.ContentRootPath, _appReleaseOptions.StorageRootPath, _appReleaseOptions.ApkFileName);


        if (!System.IO.File.Exists(apkPath))
        {
            _logger.LogWarning("指定版本 APK 檔案不存在：{ApkPath}", apkPath);
            return NotFound(new { message = $"版本 {version} 的 APK 不存在" });
        }

        try
        {
            var fileBytes = System.IO.File.ReadAllBytes(apkPath);
            _logger.LogInformation("提供指定版本 APK 下載：{FileName}，大小：{Size} bytes", fileName, fileBytes.Length);

            return File(fileBytes, "application/vnd.android.package-archive", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "讀取指定版本 APK 時發生錯誤：{ApkPath}", apkPath);
            return StatusCode(500, new { message = "讀取檔案時發生錯誤" });
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 依據目前請求資訊組合出最新 APK 下載 API 端點網址。
    /// 注意：若反向代理未正確傳遞 HTTPS，強制改為 https，避免行動端取得 http 連結。
    /// </summary>
    /// <returns>提供給行動端直接呼叫之下載 API 完整網址。</returns>
    private string BuildDownloadUrl()
    {
        // 讀取反向代理可能傳遞的協定標頭（如 Nginx/ALB）
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : Request.Scheme;

        // 若取得為 http（常見於內部容器或反向代理設定），強制升級為 https 以符合外部實際使用環境
        if (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https";
        }

        // 產生對應下載 API 之完整網址（不附加檔案名稱，因為路由不使用檔名）
        var url = Url.Action(
            action: nameof(DownloadApk),
            controller: "AppReleases",
            values: null,
            protocol: scheme,
            host: Request.Host.Value) ?? "/api/appreleases/download";

        return $"{url}/{_appReleaseOptions.ApkFileName}";
    }
}
