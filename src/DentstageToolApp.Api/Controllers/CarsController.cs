using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Cars;
using DentstageToolApp.Api.Services.Car;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 車輛維運 API，提供新增車輛的資料入口。
/// </summary>
[ApiController]
[Route("api/cars")]
[Authorize]
public class CarsController : ControllerBase
{
    private readonly ICarManagementService _carManagementService;
    private readonly ILogger<CarsController> _logger;

    /// <summary>
    /// 建構子，注入車輛維運服務與記錄器。
    /// </summary>
    public CarsController(ICarManagementService carManagementService, ILogger<CarsController> logger)
    {
        _carManagementService = carManagementService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 新增車輛資料，建立車牌、品牌、型號與備註等欄位。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateCarResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCarResponse>> CreateCarAsync([FromBody] CreateCarRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // ModelState 會帶有詳細欄位錯誤，直接回傳標準 ProblemDetails。
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _carManagementService.CreateCarAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (CarManagementException ex)
        {
            _logger.LogWarning(ex, "新增車輛失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增車輛資料失敗");
        }
        catch (OperationCanceledException)
        {
            // 前端可能在等待時取消請求，寫入記錄後回傳 499 狀態碼資訊。
            _logger.LogInformation("新增車輛流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增車輛資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增車輛流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增車輛資料失敗");
        }
    }

    /// <summary>
    /// 取得車輛品牌與型號主檔清單，供前端建立下拉選項。
    /// </summary>
    [HttpGet("/api/brands-models")]
    [ProducesResponseType(typeof(CarBrandModelListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CarBrandModelListResponse>> GetBrandModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _carManagementService.GetBrandModelsAsync(cancellationToken);
            return Ok(response);
        }
        catch (CarManagementException ex)
        {
            // 對於自訂例外，維持服務層提供的錯誤狀態與訊息。
            _logger.LogWarning(ex, "取得品牌型號失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取得品牌型號失敗");
        }
        catch (OperationCanceledException)
        {
            // 當前端取消請求時，回傳 499 表示流程被中斷。
            _logger.LogInformation("取得品牌型號流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "取得品牌型號已取消");
        }
        catch (Exception ex)
        {
            // 其餘例外統一視為系統錯誤，利於集中監控。
            _logger.LogError(ex, "取得品牌型號流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得品牌型號失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將例外轉換成 ProblemDetails，統一錯誤輸出格式。
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
