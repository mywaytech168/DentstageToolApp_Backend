using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Purchases;
using DentstageToolApp.Api.Services.Purchase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 採購單維運 API，提供查詢與維護採購單的端點。
/// </summary>
[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ILogger<PurchaseOrdersController> _logger;

    /// <summary>
    /// 建構子，注入採購服務與記錄器。
    /// </summary>
    public PurchaseOrdersController(IPurchaseService purchaseService, ILogger<PurchaseOrdersController> logger)
    {
        _purchaseService = purchaseService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得採購單列表。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PurchaseOrderListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PurchaseOrderListResponse>> GetPurchaseOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _purchaseService.GetPurchaseOrdersAsync(cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "取得採購單列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢採購單列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得採購單列表流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢採購單列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得採購單列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢採購單列表失敗");
        }
    }

    /// <summary>
    /// 取得單筆採購單明細。
    /// </summary>
    [HttpGet("{purchaseOrderUid}")]
    [ProducesResponseType(typeof(PurchaseOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> GetPurchaseOrderAsync(string purchaseOrderUid, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _purchaseService.GetPurchaseOrderAsync(purchaseOrderUid, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "取得採購單明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢採購單資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得採購單明細流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢採購單資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得採購單明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢採購單資料失敗");
        }
    }

    /// <summary>
    /// 新增採購單。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> CreatePurchaseOrderAsync([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _purchaseService.CreatePurchaseOrderAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "新增採購單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增採購單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增採購單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增採購單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增採購單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增採購單失敗");
        }
    }

    /// <summary>
    /// 更新採購單。
    /// </summary>
    [HttpPut("{purchaseOrderUid}")]
    [ProducesResponseType(typeof(PurchaseOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> UpdatePurchaseOrderAsync(string purchaseOrderUid, [FromBody] UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        request.PurchaseOrderUid = purchaseOrderUid;

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _purchaseService.UpdatePurchaseOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "更新採購單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "更新採購單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更新採購單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "更新採購單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新採購單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "更新採購單失敗");
        }
    }

    /// <summary>
    /// 刪除採購單。
    /// </summary>
    [HttpDelete("{purchaseOrderUid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePurchaseOrderAsync(string purchaseOrderUid, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            await _purchaseService.DeletePurchaseOrderAsync(purchaseOrderUid, operatorName, cancellationToken);
            return NoContent();
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "刪除採購單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除採購單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除採購單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除採購單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除採購單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除採購單失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一組裝 ProblemDetails 物件，提供一致的錯誤回應格式。
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
    /// 取得操作人員名稱，優先使用 displayName，再回退到使用者識別碼。
    /// </summary>
    private string GetCurrentOperatorName()
    {
        var displayName = User.FindFirstValue("displayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

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
        return string.IsNullOrWhiteSpace(userUid) ? "UnknownUser" : userUid;
    }

    // ---------- 生命週期 ----------
    // 控制器目前無需額外生命週期處理，預留區塊以符合專案規範。
}
