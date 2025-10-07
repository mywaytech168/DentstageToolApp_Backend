using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.MaintenanceOrders;
using DentstageToolApp.Api.Services.MaintenanceOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 維修單 API，提供列表查詢、詳情與狀態異動操作。
/// </summary>
[ApiController]
[Route("api/maintenance-orders")]
[Authorize]
public class MaintenanceOrdersController : ControllerBase
{
    private readonly IMaintenanceOrderService _maintenanceOrderService;
    private readonly ILogger<MaintenanceOrdersController> _logger;

    /// <summary>
    /// 建構子，注入維修單服務與記錄器。
    /// </summary>
    public MaintenanceOrdersController(IMaintenanceOrderService maintenanceOrderService, ILogger<MaintenanceOrdersController> logger)
    {
        _maintenanceOrderService = maintenanceOrderService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 以查詢參數取得維修單列表。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MaintenanceOrderListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOrderListResponse>> GetOrdersAsync([FromQuery] MaintenanceOrderListQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("查詢維修單列表，參數：{@Query}", query);

        var response = await _maintenanceOrderService.GetOrdersAsync(query, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// 透過 POST 傳遞查詢條件取得維修單列表。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MaintenanceOrderListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOrderListResponse>> SearchOrdersAsync([FromBody] MaintenanceOrderListQuery request, CancellationToken cancellationToken)
    {
        var query = request ?? new MaintenanceOrderListQuery();

        _logger.LogDebug("POST 查詢維修單列表，參數：{@Query}", query);

        var response = await _maintenanceOrderService.GetOrdersAsync(query, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// 取得單一維修單的詳細資料。
    /// </summary>
    [HttpPost("detail")]
    [ProducesResponseType(typeof(MaintenanceOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderDetailResponse>> GetOrderDetailAsync([FromBody] MaintenanceOrderDetailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _maintenanceOrderService.GetOrderAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "取得維修單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取得維修單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得維修單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未取得。", "取得維修單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得維修單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得維修單失敗");
        }
    }

    /// <summary>
    /// 將維修單狀態回溯並同步恢復估價單狀態。
    /// </summary>
    [HttpPost("revert")]
    [ProducesResponseType(typeof(MaintenanceOrderStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderStatusChangeResponse>> RevertOrderAsync([FromBody] MaintenanceOrderRevertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.RevertOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "維修單狀態回溯失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "維修單狀態回溯失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("維修單狀態回溯流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "維修單狀態回溯取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "維修單狀態回溯流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "維修單狀態回溯失敗");
        }
    }

    /// <summary>
    /// 確認維修開始，將維修單狀態改為維修中。
    /// </summary>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(MaintenanceOrderStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderStatusChangeResponse>> ConfirmMaintenanceAsync([FromBody] MaintenanceOrderConfirmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.ConfirmMaintenanceAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "確認維修失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "確認維修失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("確認維修流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "確認維修取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "確認維修流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "確認維修失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將例外轉換為 ProblemDetails，統一輸出格式。
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
    /// 從 JWT 權杖中取出操作人員名稱，優先使用 displayName。
    /// </summary>
    private string GetCurrentOperatorName()
    {
        var displayName = User.FindFirstValue("displayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        var nameClaim = User.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(nameClaim))
        {
            return nameClaim;
        }

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return sub;
        }

        return "UnknownUser";
    }
}
