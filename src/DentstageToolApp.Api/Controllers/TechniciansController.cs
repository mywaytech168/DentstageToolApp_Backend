using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentstageToolApp.Api.Technicians;
using DentstageToolApp.Api.Services.Technician;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 技師資料查詢 API，提供前端取得登入者所屬門市的技師名單。
/// </summary>
[ApiController]
[Route("api/technicians")]
[Authorize]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianQueryService _technicianQueryService;
    private readonly ILogger<TechniciansController> _logger;

    /// <summary>
    /// 建構子，注入技師查詢服務與記錄器。
    /// </summary>
    public TechniciansController(
        ITechnicianQueryService technicianQueryService,
        ILogger<TechniciansController> logger)
    {
        _technicianQueryService = technicianQueryService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得目前登入者所屬門市的技師名單，供前端建立下拉選單使用。
    /// </summary>
    /// <param name="cancellationToken">取消權杖，供前端取消請求。</param>
    [HttpGet]
    [ProducesResponseType(typeof(TechnicianListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TechnicianListResponse>> GetTechniciansAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 由 JWT 取得目前登入者識別碼，並作為查詢門市與技師的依據。
            var userUid = GetCurrentUserUid();
            if (string.IsNullOrWhiteSpace(userUid))
            {
                _logger.LogWarning("JWT 欠缺使用者識別碼，無法查詢技師名單。");
                return BuildProblemDetails(HttpStatusCode.Unauthorized, "驗證資訊缺少使用者識別碼，請重新登入後再試。", "查詢技師名單失敗");
            }

            _logger.LogDebug("查詢使用者 {UserUid} 所屬門市的技師名單。", userUid);
            var response = await _technicianQueryService.GetTechniciansAsync(userUid, cancellationToken);
            return Ok(response);
        }
        catch (TechnicianQueryServiceException ex)
        {
            // 已知的服務例外轉換為 ProblemDetails，保留原始狀態碼與訊息。
            _logger.LogWarning(ex, "查詢技師名單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢技師名單失敗");
        }
        catch (OperationCanceledException)
        {
            // 前端若取消請求，統一回應 499 狀態碼資訊，讓前端得知流程被中止。
            _logger.LogInformation("查詢技師名單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢技師名單已取消");
        }
        catch (Exception ex)
        {
            // 其他未預期錯誤以 500 形式告知前端稍後再試。
            _logger.LogError(ex, "查詢技師名單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢技師名單失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將錯誤訊息組裝為 ProblemDetails，統一錯誤回應格式。
    /// </summary>
    private ActionResult BuildProblemDetails(HttpStatusCode statusCode, string message, string title)
    {
        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = message,
            Instance = HttpContext.Request.Path
        };

        return StatusCode(problem.Status ?? StatusCodes.Status500InternalServerError, problem);
    }

    /// <summary>
    /// 從 JWT 權杖中解析目前登入者的唯一識別碼，作為查詢門市的憑證。
    /// </summary>
    private string? GetCurrentUserUid()
    {
        // 依序使用常見的使用者識別 Claims，確保舊版與新版權杖皆能被支援。
        var userUid = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!string.IsNullOrWhiteSpace(userUid))
        {
            return userUid;
        }

        userUid = User.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
        if (!string.IsNullOrWhiteSpace(userUid))
        {
            return userUid;
        }

        userUid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userUid) ? null : userUid;
    }

    // ---------- 生命週期 ----------
    // 控制器目前沒有額外生命週期事件，保留區塊以符合專案規範。
}
