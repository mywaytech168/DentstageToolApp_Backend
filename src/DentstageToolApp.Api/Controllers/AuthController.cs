using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using DentstageToolApp.Api.Auth;
using DentstageToolApp.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 身份驗證相關 API 控制器，負責處理登入與 Token 更新流程。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// 建構子，注入身份驗證服務與記錄器。
    /// </summary>
    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 透過裝置機碼進行登入，若驗證成功則回傳權杖。
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // 參數驗證失敗時，直接回傳 400 詳細錯誤
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            _logger.LogWarning(ex, "登入失敗：{Message}", ex.Message);
            return BuildAuthErrorResponse(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登入流程發生未預期錯誤。");
            return BuildAuthErrorResponse(HttpStatusCode.InternalServerError, "系統處理登入時發生錯誤，請稍後再試。");
        }
    }

    /// <summary>
    /// 透過 Refresh Token 換取新的 Access Token。
    /// </summary>
    [HttpPost("token/refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _authService.RefreshTokenAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            _logger.LogWarning(ex, "Refresh Token 失敗：{Message}", ex.Message);
            return BuildAuthErrorResponse(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 Token 時發生未預期錯誤。");
            return BuildAuthErrorResponse(HttpStatusCode.InternalServerError, "系統處理 Token 更新時發生錯誤，請稍後再試。");
        }
    }

    /// <summary>
    /// 查詢目前登入者的顯示名稱與角色資訊。
    /// </summary>
    [HttpGet("info")]
    [Authorize]
    [ProducesResponseType(typeof(AuthInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthInfoResponse>> GetUserInfo(CancellationToken cancellationToken)
    {
        // 透過 JWT Claims 取得使用者識別碼，避免前端額外傳遞參數
        var userUid = GetCurrentUserUid();

        if (string.IsNullOrWhiteSpace(userUid))
        {
            // 權杖缺少識別碼時直接回覆未授權，提醒重新登入
            return BuildAuthErrorResponse(HttpStatusCode.Unauthorized, "無法取得登入資訊，請重新登入後再試。");
        }

        try
        {
            // 呼叫服務查詢顯示名稱與角色，統一由資料庫取值
            var response = await _authService.GetUserInfoAsync(userUid, cancellationToken);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            _logger.LogWarning(ex, "查詢登入者資訊失敗：{Message}", ex.Message);
            return BuildAuthErrorResponse(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢登入者資訊時發生未預期錯誤。");
            return BuildAuthErrorResponse(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 封裝錯誤回應格式，統一輸出 ProblemDetails。
    /// </summary>
    private ActionResult BuildAuthErrorResponse(HttpStatusCode statusCode, string message)
    {
        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = "身份驗證失敗",
            Detail = message,
            Instance = HttpContext.Request.Path
        };

        return StatusCode(problem.Status.Value, problem);
    }

    /// <summary>
    /// 從 JWT Claims 中解析使用者唯一識別碼，支援常見的 Claim 名稱。
    /// </summary>
    private string? GetCurrentUserUid()
    {
        // 依序嘗試 Sub 與 UniqueName，確保與權杖簽發邏輯相容
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

        // 最後回退至一般 NameIdentifier，提升與其他系統整合的彈性
        userUid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userUid) ? null : userUid;
    }

    // ---------- 生命週期 ----------
    // 控制器沒有額外生命週期事件，保留此區塊供未來擴充使用。
}
