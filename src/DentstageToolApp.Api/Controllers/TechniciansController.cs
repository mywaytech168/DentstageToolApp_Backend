using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Technicians;
using DentstageToolApp.Api.Services.Technician;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 技師資料查詢 API，提供前端取得特定店家的技師名單。
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
    /// 取得指定店家的技師名單，供前端建立下拉選單使用。
    /// </summary>
    /// <param name="query">查詢參數，需包含店家識別碼。</param>
    /// <param name="cancellationToken">取消權杖，供前端取消請求。</param>
    [HttpGet]
    [ProducesResponseType(typeof(TechnicianListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TechnicianListResponse>> GetTechniciansAsync([FromQuery] TechnicianListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // 若查詢參數未符合驗證條件，直接回傳標準 ProblemDetails 供前端顯示。
            return ValidationProblem(ModelState);
        }

        try
        {
            _logger.LogDebug("查詢店家 {StoreId} 的技師名單。", query.StoreId);
            var response = await _technicianQueryService.GetTechniciansAsync(query, cancellationToken);
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

    // ---------- 生命週期 ----------
    // 控制器目前沒有額外生命週期事件，保留區塊以符合專案規範。
}
