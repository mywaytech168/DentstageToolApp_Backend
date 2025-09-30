using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Permissions;
using DentstageToolApp.Api.Services.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 權限選單控制器，目前僅提供查詢使用者角色字串的需求。
/// </summary>
[ApiController]
[Route("api/permission-menus")]
public class PermissionMenusController : ControllerBase
{
    private readonly IAccountAdminService _accountAdminService;
    private readonly ILogger<PermissionMenusController> _logger;

    /// <summary>
    /// 建構子，注入帳號服務與記錄器。
    /// </summary>
    public PermissionMenusController(
        IAccountAdminService accountAdminService,
        ILogger<PermissionMenusController> logger)
    {
        _accountAdminService = accountAdminService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得指定帳號的角色資訊，供前端決定顯示哪些選單項目。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PermissionRoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionRoleResponse>> GetRole([FromQuery] string? userUid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userUid))
        {
            // 未提供 userUid 時直接回傳 400，避免送出無效查詢。
            return BuildErrorResponse(HttpStatusCode.BadRequest, "請提供使用者識別碼以取得角色資訊。", "取得角色資訊失敗");
        }

        try
        {
            // 查詢帳號資料並擷取角色字串回傳給前端。
            var account = await _accountAdminService.GetAccountAsync(userUid, cancellationToken);
            var response = new PermissionRoleResponse
            {
                Role = account.Role
            };

            return Ok(response);
        }
        catch (AccountAdminException ex)
        {
            _logger.LogWarning(ex, "查詢角色資訊失敗：{Message}", ex.Message);
            return BuildErrorResponse(ex.StatusCode, ex.Message, "取得角色資訊失敗");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢角色資訊流程發生未預期錯誤。");
            return BuildErrorResponse(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得角色資訊失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一建立 ProblemDetails，確保錯誤訊息格式一致。
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
