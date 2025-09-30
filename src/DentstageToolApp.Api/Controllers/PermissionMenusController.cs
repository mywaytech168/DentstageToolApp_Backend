using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Permissions;
using DentstageToolApp.Api.Services.Admin;
using DentstageToolApp.Api.Services.Permissions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 帳號權限選單控制器，根據店家型態派送對應功能清單。
/// </summary>
[ApiController]
[Route("api/permission-menus")]
public class PermissionMenusController : ControllerBase
{
    private readonly IPermissionMenuService _permissionMenuService;
    private readonly IAccountAdminService _accountAdminService;
    private readonly ILogger<PermissionMenusController> _logger;

    /// <summary>
    /// 建構子，注入權限選單服務與帳號服務。
    /// </summary>
    public PermissionMenusController(
        IPermissionMenuService permissionMenuService,
        IAccountAdminService accountAdminService,
        ILogger<PermissionMenusController> logger)
    {
        _permissionMenuService = permissionMenuService;
        _accountAdminService = accountAdminService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得指定店家型態可用的權限選單。若未提供店型，需透過使用者識別碼推論。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PermissionMenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PermissionMenuItemDto>>> GetMenus([FromQuery] string? storeType, [FromQuery] string? userUid, CancellationToken cancellationToken)
    {
        try
        {
            string resolvedStoreType;
            string? resolvedRole = null;

            if (!string.IsNullOrWhiteSpace(storeType))
            {
                // 直接使用傳入的店型字串，支援查詢特定選單。
                resolvedStoreType = storeType;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(userUid))
                {
                    return BuildErrorResponse(HttpStatusCode.BadRequest, "請提供店家型態或使用者識別碼，以取得權限選單。", "取得權限選單失敗");
                }

                // 透過帳號資訊推論店型，確保與前端顯示一致。
                var account = await _accountAdminService.GetAccountAsync(userUid, cancellationToken);
                resolvedStoreType = account.Store.StoreType ?? account.Role ?? string.Empty;
                resolvedRole = account.Role;
            }

            var menus = await _permissionMenuService.GetMenuByStoreTypeAsync(resolvedStoreType, resolvedRole, cancellationToken);
            return Ok(menus);
        }
        catch (AccountAdminException ex)
        {
            _logger.LogWarning(ex, "透過帳號取得選單失敗：{Message}", ex.Message);
            return BuildErrorResponse(ex.StatusCode, ex.Message, "取得權限選單失敗");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢權限選單發生未預期錯誤。");
            return BuildErrorResponse(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得權限選單失敗");
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
