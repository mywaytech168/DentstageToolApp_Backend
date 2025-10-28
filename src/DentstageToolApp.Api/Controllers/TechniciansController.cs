using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Technicians;
using DentstageToolApp.Api.Services.Technician;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 技師資料維運 API，提供查詢、建立、更新與刪除技師資料的端點。
/// </summary>
[ApiController]
[Route("api/technicians")]
[Authorize]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianManagementService _technicianManagementService;
    private readonly ITechnicianQueryService _technicianQueryService;
    private readonly ILogger<TechniciansController> _logger;

    /// <summary>
    /// 建構子，注入技師維運服務與記錄器。
    /// </summary>
    public TechniciansController(
        ITechnicianManagementService technicianManagementService,
        ITechnicianQueryService technicianQueryService,
        ILogger<TechniciansController> logger)
    {
        _technicianManagementService = technicianManagementService;
        _technicianQueryService = technicianQueryService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得目前登入者所屬門市的技師名單，供前端建立下拉選單使用。
    /// </summary>
    /// <remarks>
    /// GET /api/technicians?page=1&amp;pageSize=20
    /// </remarks>
    /// <param name="pagination">分頁條件，預設第一頁、每頁二十筆。</param>
    /// <param name="cancellationToken">取消權杖，供前端取消請求。</param>
    [HttpGet]
    [ProducesResponseType(typeof(TechnicianListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TechnicianListResponse>> GetTechniciansAsync(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        try
        {
            // 由 JWT 取得目前登入者識別碼，並作為查詢門市與技師的依據。
            var userUid = GetCurrentUserUid();
            if (string.IsNullOrWhiteSpace(userUid))
            {
                _logger.LogWarning("JWT 欠缺使用者識別碼，無法查詢技師名單。");
                return BuildProblemDetails(HttpStatusCode.Unauthorized, "驗證資訊缺少使用者識別碼，請重新登入後再試。", "查詢技師名單失敗");
            }

            _logger.LogDebug("查詢使用者 {UserUid} 所屬門市的技師名單。", userUid);
            var paginationRequest = pagination ?? new PaginationRequest();
            var response = await _technicianQueryService.GetTechniciansAsync(userUid, paginationRequest, cancellationToken);
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

    /// <summary>
    /// 依指定門市識別碼取得技師名單，提供跨門市查詢需求。
    /// </summary>
    /// <param name="storeUid">欲查詢的門市識別碼。</param>
    /// <remarks>
    /// GET /api/technicians/St_SAMPLE_UID?page=1&amp;pageSize=20
    /// </remarks>
    /// <param name="pagination">分頁條件，預設第一頁、每頁二十筆。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet("{storeUid}")]
    [ProducesResponseType(typeof(TechnicianListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TechnicianListResponse>> GetTechniciansByStoreAsync(
        string storeUid,
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        try
        {
            var paginationRequest = pagination ?? new PaginationRequest();
            var response = await _technicianQueryService.GetTechniciansByStoreAsync(storeUid, paginationRequest, cancellationToken);
            return Ok(response);
        }
        catch (TechnicianQueryServiceException ex)
        {
            _logger.LogWarning(ex, "依門市查詢技師名單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢技師名單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("依門市查詢技師名單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢技師名單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "依門市查詢技師名單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢技師名單失敗");
        }
    }

    /// <summary>
    /// 新增技師資料，並綁定至指定門市。
    /// </summary>
    /// <param name="request">技師建立資料。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "technicianName": "王小明",
          "jobTitle": "資深技師",
          "storeUid": "St_28E50A91-6DA5-4A66-9BA9-6C318D2A9E12"
        }
        """)]
    [ProducesResponseType(typeof(CreateTechnicianResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateTechnicianResponse>> CreateTechnicianAsync([FromBody] CreateTechnicianRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 以 JWT 取得操作人員名稱，便於記錄操作歷程。
            var operatorName = GetCurrentOperatorName();
            var response = await _technicianManagementService.CreateTechnicianAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (TechnicianManagementException ex)
        {
            // 將已知業務例外轉換為統一錯誤輸出格式。
            _logger.LogWarning(ex, "新增技師失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增技師資料失敗");
        }
        catch (OperationCanceledException)
        {
            // 請求若被取消，回傳 499 告知前端流程已終止。
            _logger.LogInformation("新增技師流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增技師資料已取消");
        }
        catch (Exception ex)
        {
            // 其他未預期錯誤以 500 告知前端稍後再試。
            _logger.LogError(ex, "新增技師流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增技師資料失敗");
        }
    }

    /// <summary>
    /// 編輯既有技師資料，包含名稱、職稱與所屬門市。
    /// </summary>
    /// <param name="request">技師更新資料。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "technicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
          "technicianName": "測試技師",
          "jobTitle": "主任技師"
        }
        """)]
    [ProducesResponseType(typeof(EditTechnicianResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditTechnicianResponse>> EditTechnicianAsync([FromBody] EditTechnicianRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 取得操作人員資訊，便於寫入操作紀錄。
            var operatorName = GetCurrentOperatorName();
            var response = await _technicianManagementService.EditTechnicianAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (TechnicianManagementException ex)
        {
            _logger.LogWarning(ex, "編輯技師失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯技師資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯技師流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯技師資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯技師流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯技師資料失敗");
        }
    }

    /// <summary>
    /// 刪除指定技師資料，刪除前會檢查是否仍被報價單使用。
    /// </summary>
    /// <param name="request">技師刪除資料。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpPost("delete")]
    [SwaggerMockRequestExample(
        """
        {
          "technicianUid": "Tc_DELETE_TARGET_UID"
        }
        """)]
    [ProducesResponseType(typeof(DeleteTechnicianResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteTechnicianResponse>> DeleteTechnicianAsync([FromBody] DeleteTechnicianRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 取得操作人員名稱，記錄刪除紀錄。
            var operatorName = GetCurrentOperatorName();
            var response = await _technicianManagementService.DeleteTechnicianAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (TechnicianManagementException ex)
        {
            _logger.LogWarning(ex, "刪除技師失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除技師資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除技師流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除技師資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除技師流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除技師資料失敗");
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

    /// <summary>
    /// 從 JWT 權杖中解析目前登入者的唯一識別碼，作為查詢門市的憑證。
    /// </summary>
    private string? GetCurrentUserUid()
    {
        // 依序使用常見的使用者識別 Claims，確保舊版與新版權杖皆能被支援。
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
        return string.IsNullOrWhiteSpace(userUid) ? null : userUid;
    }

    /// <summary>
    /// 取得操作人員名稱，優先使用 displayName，若無則回退至識別碼。
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
