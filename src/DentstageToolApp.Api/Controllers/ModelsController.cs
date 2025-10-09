using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models;
using DentstageToolApp.Api.Services.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 車型維運 API，提供新增、編輯與刪除車型的端點。
/// </summary>
[ApiController]
[Route("api/models")]
[Authorize]
public class ModelsController : ControllerBase
{
    private readonly IModelManagementService _modelManagementService;
    private readonly ILogger<ModelsController> _logger;

    /// <summary>
    /// 建構子，注入車型維運服務與記錄器。
    /// </summary>
    public ModelsController(IModelManagementService modelManagementService, ILogger<ModelsController> logger)
    {
        _modelManagementService = modelManagementService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 新增車型資料。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateModelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateModelResponse>> CreateModelAsync([FromBody] CreateModelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _modelManagementService.CreateModelAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ModelManagementException ex)
        {
            _logger.LogWarning(ex, "新增車型失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增車型資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增車型流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增車型資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增車型流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增車型資料失敗");
        }
    }

    /// <summary>
    /// 編輯車型資料。
    /// </summary>
    [HttpPost("edit")]
    [ProducesResponseType(typeof(EditModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditModelResponse>> EditModelAsync([FromBody] EditModelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _modelManagementService.EditModelAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (ModelManagementException ex)
        {
            _logger.LogWarning(ex, "編輯車型失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯車型資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯車型流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯車型資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯車型流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯車型資料失敗");
        }
    }

    /// <summary>
    /// 刪除車型資料。
    /// </summary>
    [HttpPost("delete")]
    [ProducesResponseType(typeof(DeleteModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteModelResponse>> DeleteModelAsync([FromBody] DeleteModelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _modelManagementService.DeleteModelAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (ModelManagementException ex)
        {
            _logger.LogWarning(ex, "刪除車型失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除車型資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除車型流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除車型資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除車型流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除車型資料失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 組裝 ProblemDetails 物件，提供統一的錯誤格式。
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
    /// 取得目前操作人員名稱，優先顯示 displayName，再回退至識別碼。
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
