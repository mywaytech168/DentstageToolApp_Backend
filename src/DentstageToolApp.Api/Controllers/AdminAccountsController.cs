using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Admin;
using DentstageToolApp.Api.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 管理者帳號維運 API，提供建立帳號與裝置的功能。
/// </summary>
[ApiController]
[Route("api/admin/accounts")]
public class AdminAccountsController : ControllerBase
{
    private readonly IAccountAdminService _accountAdminService;
    private readonly ILogger<AdminAccountsController> _logger;

    /// <summary>
    /// 建構子，注入管理者帳號服務與記錄器。
    /// </summary>
    public AdminAccountsController(IAccountAdminService accountAdminService, ILogger<AdminAccountsController> logger)
    {
        _accountAdminService = accountAdminService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 建立新的使用者帳號與對應裝置機碼。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserDeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateUserDeviceResponse>> CreateAccountWithDevice([FromBody] CreateUserDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // 回傳 400 並附帶詳細驗證錯誤
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _accountAdminService.CreateUserWithDeviceAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (AccountAdminException ex)
        {
            _logger.LogWarning(ex, "建立帳號失敗：{Message}", ex.Message);
            return BuildErrorResponse(ex.StatusCode, ex.Message, "建立帳號失敗");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立帳號流程發生未預期錯誤。");
            return BuildErrorResponse(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "建立帳號失敗");
        }
    }

    /// <summary>
    /// 依 JWT 權杖內的使用者識別碼查詢個人帳號資訊，回傳 DisplayName 與 Role。
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AdminAccountDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminAccountDetailResponse>> GetAccountDetail(CancellationToken cancellationToken)
    {
        // 透過 JWT 取得當前登入者的唯一識別碼，避免前端需要額外提供 userUid。
        var userUid = GetCurrentUserUid();

        if (string.IsNullOrWhiteSpace(userUid))
        {
            // 若權杖未帶出識別碼，直接提示重新登入。
            return BuildErrorResponse(HttpStatusCode.Unauthorized, "無法取得登入者資訊，請重新登入後再試。", "查詢帳號失敗");
        }

        try
        {
            // 呼叫服務取得帳號基本資料，提供前端顯示顯示名稱與角色。
            var response = await _accountAdminService.GetAccountAsync(userUid, cancellationToken);
            return Ok(response);
        }
        catch (AccountAdminException ex)
        {
            _logger.LogWarning(ex, "查詢帳號失敗：{Message}", ex.Message);
            return BuildErrorResponse(ex.StatusCode, ex.Message, "查詢帳號失敗");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢帳號流程發生未預期錯誤。");
            return BuildErrorResponse(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢帳號失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一建立 ProblemDetails 物件，確保錯誤輸出一致。
    /// </summary>
    private ActionResult BuildErrorResponse(HttpStatusCode statusCode, string message, string title)
    {
        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = message,
            Instance = HttpContext.Request.Path
        };

        return StatusCode(problem.Status.Value, problem);
    }

    /// <summary>
    /// 從目前的 ClaimsPrincipal 解析使用者唯一識別碼，支援多種常見 Claim 名稱。
    /// </summary>
    private string? GetCurrentUserUid()
    {
        // 依序嘗試 JWT 的 Sub 與 UniqueName，確保與簽發邏輯一致。
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

        // 最後回退至一般 NameIdentifier，提升相容性。
        userUid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userUid) ? null : userUid;
    }

    // ---------- 生命週期 ----------
    // 控制器目前無額外生命週期事件，保留區塊供未來擴充。
}
