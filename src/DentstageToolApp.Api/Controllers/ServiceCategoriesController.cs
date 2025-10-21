using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.ServiceCategories;
using DentstageToolApp.Api.Services.ServiceCategory;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 服務類別維運 API，提供新增、編輯與刪除服務類別的操作端點。
/// </summary>
[ApiController]
[Route("api/service-categories")]
[Authorize]
public class ServiceCategoriesController : ControllerBase
{
    private readonly IServiceCategoryManagementService _serviceCategoryManagementService;
    private readonly IServiceCategoryQueryService _serviceCategoryQueryService;
    private readonly ILogger<ServiceCategoriesController> _logger;

    /// <summary>
    /// 建構子，注入服務類別維運服務與記錄器。
    /// </summary>
    public ServiceCategoriesController(
        IServiceCategoryManagementService serviceCategoryManagementService,
        IServiceCategoryQueryService serviceCategoryQueryService,
        ILogger<ServiceCategoriesController> logger)
    {
        _serviceCategoryManagementService = serviceCategoryManagementService;
        _serviceCategoryQueryService = serviceCategoryQueryService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得所有服務類別列表。
    /// </summary>
    /// <remarks>
    /// GET /api/service-categories?page=1&amp;pageSize=20
    /// </remarks>
    /// <param name="pagination">分頁條件，預設第一頁、每頁二十筆。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceCategoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceCategoryListResponse>> GetServiceCategoriesAsync(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        try
        {
            var paginationRequest = pagination ?? new PaginationRequest();
            var response = await _serviceCategoryQueryService.GetServiceCategoriesAsync(paginationRequest, cancellationToken);
            return Ok(response);
        }
        catch (ServiceCategoryQueryException ex)
        {
            _logger.LogWarning(ex, "取得服務類別列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢服務類別列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("服務類別列表查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢服務類別列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得服務類別列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢服務類別列表失敗");
        }
    }

    /// <summary>
    /// 透過維修類型中文標籤取得服務類別詳細資料。
    /// </summary>
    /// <param name="fixType">維修類型中文標籤。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet("{fixType}")]
    [ProducesResponseType(typeof(ServiceCategoryDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceCategoryDetailResponse>> GetServiceCategoryAsync(string fixType, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _serviceCategoryQueryService.GetServiceCategoryAsync(fixType, cancellationToken);
            return Ok(response);
        }
        catch (ServiceCategoryQueryException ex)
        {
            _logger.LogWarning(ex, "取得服務類別明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢服務類別資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("服務類別明細查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢服務類別資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得服務類別明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢服務類別資料失敗");
        }
    }

    /// <summary>
    /// 新增服務類別資料。
    /// </summary>
    [HttpPost]
    // 加入範例以指引前端填寫 categoryName 欄位，確保 Swagger 文件一致性。
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "凹痕",
          "categoryName": "凹痕"
        }
        """)]
    [ProducesResponseType(typeof(CreateServiceCategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateServiceCategoryResponse>> CreateServiceCategoryAsync([FromBody] CreateServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _serviceCategoryManagementService.CreateServiceCategoryAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ServiceCategoryManagementException ex)
        {
            _logger.LogWarning(ex, "新增服務類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增服務類別資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增服務類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增服務類別資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增服務類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增服務類別資料失敗");
        }
    }

    /// <summary>
    /// 編輯服務類別資料。
    /// </summary>
    [HttpPost("edit")]
    // 呈現需帶入的 fixType 與更新後名稱，協助 Swagger 使用者理解欄位需求。
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "paint",
          "categoryName": "鈑烤"
        }
        """)]
    [ProducesResponseType(typeof(EditServiceCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditServiceCategoryResponse>> EditServiceCategoryAsync([FromBody] EditServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _serviceCategoryManagementService.EditServiceCategoryAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (ServiceCategoryManagementException ex)
        {
            _logger.LogWarning(ex, "編輯服務類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯服務類別資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯服務類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯服務類別資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯服務類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯服務類別資料失敗");
        }
    }

    /// <summary>
    /// 刪除服務類別資料。
    /// </summary>
    [HttpPost("delete")]
    // 利用範例明確告知僅需提供維修類型中文標籤即可完成刪除動作。
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "other"
        }
        """)]
    [ProducesResponseType(typeof(DeleteServiceCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteServiceCategoryResponse>> DeleteServiceCategoryAsync([FromBody] DeleteServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _serviceCategoryManagementService.DeleteServiceCategoryAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (ServiceCategoryManagementException ex)
        {
            _logger.LogWarning(ex, "刪除服務類別失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除服務類別資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除服務類別流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除服務類別資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除服務類別流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除服務類別資料失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 封裝 ProblemDetails，統一錯誤輸出格式。
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
    /// 取得操作人員名稱，優先顯示 displayName，再回退至識別碼。
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
