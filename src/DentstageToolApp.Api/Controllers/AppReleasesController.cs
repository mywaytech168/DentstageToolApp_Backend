using System;
using DentstageToolApp.Api.Models.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// APP 版本控制器，提供查詢最新 APK 版本資訊的端點。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AppReleasesController : ControllerBase
{
    private readonly AppReleaseOptions _appReleaseOptions;

    /// <summary>
    /// 建構式注入版本組態，方便統一由 appsettings 管理版本資訊。
    /// </summary>
    /// <param name="appReleaseOptions">APP 版本設定資料。</param>
    public AppReleasesController(IOptions<AppReleaseOptions> appReleaseOptions)
    {
        // 直接取出組態內容供後續使用，若缺少值則於啟動時即會拋例外
        _appReleaseOptions = appReleaseOptions.Value;
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
            isForceUpdate = _appReleaseOptions.IsForceUpdate,
            downloadUrl = BuildDownloadUrl(),
            shouldUpdate
        };

        return Ok(response);
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 依據目前請求資訊與組態組合出 APK 下載網址。
    /// </summary>
    /// <returns>可直接提供給行動端下載的完整網址。</returns>
    private string BuildDownloadUrl()
    {
        // 若請求內缺少主機資訊，則回傳相對路徑確保至少可在文件中參考
        if (string.IsNullOrWhiteSpace(_appReleaseOptions.ApkFileName))
        {
            return string.Empty;
        }

        var requestPath = (_appReleaseOptions.DownloadBasePath ?? string.Empty).Trim();
        if (!requestPath.StartsWith('/'))
        {
            requestPath = $"/{requestPath.Trim('/')}";
        }
        else
        {
            requestPath = $"/{requestPath.Trim('/')}";
        }

        if (string.IsNullOrWhiteSpace(requestPath) || requestPath == "/")
        {
            requestPath = "/downloads/apk";
        }

        var encodedFileName = Uri.EscapeDataString(_appReleaseOptions.ApkFileName);
        var relativeUrl = $"{requestPath}/{encodedFileName}";

        if (Request is { Scheme: { } scheme, Host: { HasValue: true } host })
        {
            // 使用目前請求的 Scheme 與 Host 組合成完整網址，確保部署於反向代理時仍可取得正確路徑
            return $"{scheme}://{host}{relativeUrl}";
        }

        return relativeUrl;
    }
}
