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
/// 品牌與型號查詢 API 控制器，專責提供品牌型號資料。
/// </summary>
[ApiController]
[Route("api/brands-models")]
[Authorize]
public class BrandModelsController : ControllerBase
{
    private readonly ICarManagementService _carManagementService;
    private readonly ILogger<BrandModelsController> _logger;

    /// <summary>
    /// 建構子，注入車輛維運服務與記錄器以利記錄例外。
    /// </summary>
    public BrandModelsController(ICarManagementService carManagementService, ILogger<BrandModelsController> logger)
    {
        _carManagementService = carManagementService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得品牌與型號清單，供前端建立下拉選項使用。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CarBrandModelListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CarBrandModelListResponse>> GetBrandModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 委派服務層查詢品牌與型號主檔，保留排序與資料組裝邏輯。
            var response = await _carManagementService.GetBrandModelsAsync(cancellationToken);
            return Ok(response);
        }
        catch (CarManagementException ex)
        {
            // 對自訂例外保留原始狀態碼與訊息，便於前端判斷錯誤。
            _logger.LogWarning(ex, "取得品牌型號失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取得品牌型號失敗");
        }
        catch (OperationCanceledException)
        {
            // 使用 499 狀態碼回報前端取消，維持前後端溝通一致性。
            _logger.LogInformation("取得品牌型號流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "取得品牌型號已取消");
        }
        catch (Exception ex)
        {
            // 針對未預期錯誤寫入記錄並回傳 500，利於後續追蹤。
            _logger.LogError(ex, "取得品牌型號流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得品牌型號失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 封裝 ProblemDetails 輸出，確保錯誤格式一致。
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

