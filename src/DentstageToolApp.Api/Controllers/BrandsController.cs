using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Brands;
using DentstageToolApp.Api.Services.Brand;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 車輛品牌維運 API，提供新增、編輯與刪除品牌的操作端點。
/// </summary>
[ApiController]
[Route("api/brands")]
[Authorize]
public class BrandsController : ControllerBase
{
    private readonly IBrandManagementService _brandManagementService;
    private readonly IBrandQueryService _brandQueryService;
    private readonly ILogger<BrandsController> _logger;

    /// <summary>
    /// 建構子，注入品牌維運服務與記錄器。
    /// </summary>
    public BrandsController(
        IBrandManagementService brandManagementService,
        IBrandQueryService brandQueryService,
        ILogger<BrandsController> logger)
    {
        _brandManagementService = brandManagementService;
        _brandQueryService = brandQueryService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得品牌列表，供前端顯示品牌下拉選單使用，回應會直接帶出車型清單。
    /// </summary>
    /// <remarks>
    /// GET /api/brands?page=1&amp;pageSize=20
    /// </remarks>
    /// <param name="pagination">分頁條件。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet]
    [ProducesResponseType(typeof(BrandListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BrandListResponse>> GetBrandsAsync(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        try
        {
            var paginationRequest = pagination ?? new PaginationRequest();
            var response = await _brandQueryService.GetBrandsAsync(paginationRequest, cancellationToken);
            return Ok(response);
        }
        catch (BrandQueryServiceException ex)
        {
            _logger.LogWarning(ex, "取得品牌列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢品牌列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("品牌列表查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢品牌列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得品牌列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢品牌列表失敗");
        }
    }

    /// <summary>
    /// 透過品牌識別碼取得品牌詳細資訊。
    /// </summary>
    /// <param name="brandUid">品牌識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet("{brandUid}")]
    [ProducesResponseType(typeof(BrandDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BrandDetailResponse>> GetBrandAsync(string brandUid, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _brandQueryService.GetBrandAsync(brandUid, cancellationToken);
            return Ok(response);
        }
        catch (BrandQueryServiceException ex)
        {
            _logger.LogWarning(ex, "取得品牌明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢品牌資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("品牌明細查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢品牌資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得品牌明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢品牌資料失敗");
        }
    }

    /// <summary>
    /// 新增品牌資料，建立品牌名稱。
    /// </summary>
    [HttpPost]
    // 以 SwaggerMockRequestExample 補上新增品牌的範例，讓 Swagger 介面顯示可直接複製的欄位格式。
    [SwaggerMockRequestExample(
        """
        {
          "brandName": "DentStage 維修品牌"
        }
        """)]
    [ProducesResponseType(typeof(CreateBrandResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateBrandResponse>> CreateBrandAsync([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _brandManagementService.CreateBrandAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (BrandManagementException ex)
        {
            _logger.LogWarning(ex, "新增品牌失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增品牌資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增品牌流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增品牌資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增品牌流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增品牌資料失敗");
        }
    }

    /// <summary>
    /// 編輯品牌資料，更新品牌名稱。
    /// </summary>
    [HttpPost("edit")]
    // 透過範例說明需帶入品牌識別碼與更新後名稱，避免 Swagger 使用者漏填欄位。
    [SwaggerMockRequestExample(
        """
        {
          "brandUid": "B_45AC5C9D-9B56-4B01-A510-72F0C28F58AA",
          "brandName": "DentStage 豪華車系"
        }
        """)]
    [ProducesResponseType(typeof(EditBrandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditBrandResponse>> EditBrandAsync([FromBody] EditBrandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _brandManagementService.EditBrandAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (BrandManagementException ex)
        {
            _logger.LogWarning(ex, "編輯品牌失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯品牌資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯品牌流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯品牌資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯品牌流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯品牌資料失敗");
        }
    }

    /// <summary>
    /// 刪除品牌資料。
    /// </summary>
    [HttpPost("delete")]
    // 提供刪除品牌時的識別碼範例，讓串接者清楚需要傳送的欄位。
    [SwaggerMockRequestExample(
        """
        {
          "brandUid": "B_45AC5C9D-9B56-4B01-A510-72F0C28F58AA"
        }
        """)]
    [ProducesResponseType(typeof(DeleteBrandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteBrandResponse>> DeleteBrandAsync([FromBody] DeleteBrandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _brandManagementService.DeleteBrandAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (BrandManagementException ex)
        {
            _logger.LogWarning(ex, "刪除品牌失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除品牌資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除品牌流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除品牌資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除品牌流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除品牌資料失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一組裝 ProblemDetails，保持錯誤輸出格式一致。
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
    /// 從 JWT 權杖中取得操作人員名稱，優先使用顯示名稱再回退至使用者識別碼。
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
    // 控制器目前沒有額外生命週期事件，保留區塊以符合專案規範。
}
