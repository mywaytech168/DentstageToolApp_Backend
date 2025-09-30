using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Admin;
using DentstageToolApp.Api.Services.Admin;
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
    /// 依使用者識別碼查詢帳號資訊，目前僅回傳 DisplayName 與 Role。
    /// </summary>
    [HttpGet("{userUid}")]
    [ProducesResponseType(typeof(AdminAccountDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminAccountDetailResponse>> GetAccountDetail(string userUid, CancellationToken cancellationToken)
    {
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

    // ---------- 生命週期 ----------
    // 控制器目前無額外生命週期事件，保留區塊供未來擴充。
}
