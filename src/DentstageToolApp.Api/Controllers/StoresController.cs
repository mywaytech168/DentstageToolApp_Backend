using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Services.Store;
using DentstageToolApp.Api.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 門市維運 API，提供新增、編輯與刪除門市的端點。
/// </summary>
[ApiController]
[Route("api/stores")]
[Authorize]
public class StoresController : ControllerBase
{
    private readonly IStoreManagementService _storeManagementService;
    private readonly IStoreQueryService _storeQueryService;
    private readonly ILogger<StoresController> _logger;

    /// <summary>
    /// 建構子，注入門市維運服務與記錄器。
    /// </summary>
    public StoresController(
        IStoreManagementService storeManagementService,
        IStoreQueryService storeQueryService,
        ILogger<StoresController> logger)
    {
        _storeManagementService = storeManagementService;
        _storeQueryService = storeQueryService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得門市列表資料。
    /// </summary>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet]
    [ProducesResponseType(typeof(StoreListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoreListResponse>> GetStoresAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _storeQueryService.GetStoresAsync(cancellationToken);
            return Ok(response);
        }
        catch (StoreQueryServiceException ex)
        {
            _logger.LogWarning(ex, "取得門市列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢門市列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("門市列表查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢門市列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得門市列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢門市列表失敗");
        }
    }

    /// <summary>
    /// 透過識別碼取得門市詳細資料。
    /// </summary>
    /// <param name="storeUid">門市識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet("{storeUid}")]
    [ProducesResponseType(typeof(StoreDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreDetailResponse>> GetStoreAsync(string storeUid, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _storeQueryService.GetStoreAsync(storeUid, cancellationToken);
            return Ok(response);
        }
        catch (StoreQueryServiceException ex)
        {
            _logger.LogWarning(ex, "取得門市明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢門市資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("門市明細查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢門市資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得門市明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢門市資料失敗");
        }
    }

    /// <summary>
    /// 新增門市資料。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateStoreResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateStoreResponse>> CreateStoreAsync([FromBody] CreateStoreRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _storeManagementService.CreateStoreAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (StoreManagementException ex)
        {
            _logger.LogWarning(ex, "新增門市失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增門市資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增門市流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增門市資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增門市流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增門市資料失敗");
        }
    }

    /// <summary>
    /// 編輯門市資料。
    /// </summary>
    [HttpPost("edit")]
    [ProducesResponseType(typeof(EditStoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditStoreResponse>> EditStoreAsync([FromBody] EditStoreRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _storeManagementService.EditStoreAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (StoreManagementException ex)
        {
            _logger.LogWarning(ex, "編輯門市失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯門市資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯門市流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯門市資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯門市流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯門市資料失敗");
        }
    }

    /// <summary>
    /// 刪除門市資料。
    /// </summary>
    [HttpPost("delete")]
    [ProducesResponseType(typeof(DeleteStoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteStoreResponse>> DeleteStoreAsync([FromBody] DeleteStoreRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _storeManagementService.DeleteStoreAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (StoreManagementException ex)
        {
            _logger.LogWarning(ex, "刪除門市失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除門市資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除門市流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除門市資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除門市流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除門市資料失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一組裝 ProblemDetails，保持錯誤格式一致。
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
