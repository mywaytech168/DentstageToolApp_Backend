using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Purchases;
using DentstageToolApp.Api.Services.Purchase;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 採購品項類別維運 API，提供列表、建立、更新與刪除功能。
/// </summary>
[ApiController]
[Route("api/purchase-categories")]
[Authorize]
public class PurchaseCategoriesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ILogger<PurchaseCategoriesController> _logger;

    /// <summary>
    /// 建構子，注入採購服務與記錄器。
    /// </summary>
    public PurchaseCategoriesController(IPurchaseService purchaseService, ILogger<PurchaseCategoriesController> logger)
    {
        _purchaseService = purchaseService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得採購品項類別列表，改為透過 POST Body 傳遞分頁條件。
    /// </summary>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "page": 1,
          "pageSize": 20
        }
        """)]
    [ProducesResponseType(typeof(PurchaseCategoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PurchaseCategoryListResponse>> GetCategoriesAsync([FromBody] PurchaseCategoryListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 服務層會依據查詢條件處理分頁，回傳統一格式。
            var response = await _purchaseService.GetCategoriesAsync(query, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "取得採購品項類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢採購品項類別失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得採購品項類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢採購品項類別已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得採購品項類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢採購品項類別失敗");
        }
    }

    /// <summary>
    /// 建立採購品項類別。
    /// </summary>
    [HttpPost("create")]
    [SwaggerMockRequestExample(
        """
        {
          "categoryName": "烤漆耗材"
        }
        """)]
    [ProducesResponseType(typeof(PurchaseCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseCategoryDto>> CreateCategoryAsync([FromBody] CreatePurchaseCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _purchaseService.CreateCategoryAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "新增採購品項類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增採購品項類別失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增採購品項類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增採購品項類別已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增採購品項類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增採購品項類別失敗");
        }
    }

    /// <summary>
    /// 更新採購品項類別，請於 Request Body 內帶入欲更新的 categoryUid。
    /// </summary>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "categoryUid": "PC_6A4D9E5F-3B24-4F9D-A19F-2F8A993CB11F",
          "categoryName": "烤漆耗材-年度版本"
        }
        """)]
    [ProducesResponseType(typeof(PurchaseCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseCategoryDto>> UpdateCategoryAsync([FromBody] UpdatePurchaseCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _purchaseService.UpdateCategoryAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "更新採購品項類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "更新採購品項類別失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更新採購品項類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "更新採購品項類別已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新採購品項類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "更新採購品項類別失敗");
        }
    }

    /// <summary>
    /// 刪除採購品項類別，UID 需由 Request Body 帶入。
    /// </summary>
    [HttpDelete]
    [SwaggerMockRequestExample(
        """
        {
          "categoryUid": "PC_6A4D9E5F-3B24-4F9D-A19F-2F8A993CB11F"
        }
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategoryAsync([FromBody] DeletePurchaseCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            await _purchaseService.DeleteCategoryAsync(request, operatorName, cancellationToken);
            return NoContent();
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "刪除採購品項類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除採購品項類別失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除採購品項類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除採購品項類別已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除採購品項類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除採購品項類別失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一組裝 ProblemDetails，維持錯誤回應格式一致。
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
    /// 取得操作人員名稱，優先顯示 displayName，否則回退到識別碼。
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
    // 控制器目前無需額外生命週期處理，保留區塊符合專案規範。
}
