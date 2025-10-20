using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Services.MaintenanceOrder;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 維修單 API，提供列表查詢、詳情與狀態異動操作。
/// </summary>
[ApiController]
[Route("api/maintenance-orders")]
[Authorize]
public class MaintenanceOrdersController : ControllerBase
{
    private readonly IMaintenanceOrderService _maintenanceOrderService;
    private readonly ILogger<MaintenanceOrdersController> _logger;

    /// <summary>
    /// 建構子，注入維修單服務與記錄器。
    /// </summary>
    public MaintenanceOrdersController(IMaintenanceOrderService maintenanceOrderService, ILogger<MaintenanceOrdersController> logger)
    {
        _maintenanceOrderService = maintenanceOrderService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 以查詢參數取得維修單列表。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MaintenanceOrderListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOrderListResponse>> GetOrdersAsync([FromQuery] MaintenanceOrderListQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("查詢維修單列表，參數：{@Query}", query);

        var response = await _maintenanceOrderService.GetOrdersAsync(query, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// 透過 POST 傳遞查詢條件取得維修單列表。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MaintenanceOrderListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOrderListResponse>> SearchOrdersAsync([FromBody] MaintenanceOrderListQuery request, CancellationToken cancellationToken)
    {
        var query = request ?? new MaintenanceOrderListQuery();

        _logger.LogDebug("POST 查詢維修單列表，參數：{@Query}", query);

        var response = await _maintenanceOrderService.GetOrdersAsync(query, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// 取得單一維修單的詳細資料。
    /// </summary>
    [HttpPost("detail")]
    [ProducesResponseType(typeof(MaintenanceOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderDetailResponse>> GetOrderDetailAsync([FromBody] MaintenanceOrderDetailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _maintenanceOrderService.GetOrderAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "取得維修單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取得維修單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得維修單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未取得。", "取得維修單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得維修單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得維修單失敗");
        }
    }

    /// <summary>
    /// 將維修單狀態回溯並同步恢復估價單狀態。
    /// </summary>
    [HttpPost("revert")]
    [ProducesResponseType(typeof(MaintenanceOrderStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderStatusChangeResponse>> RevertOrderAsync([FromBody] MaintenanceOrderRevertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.RevertOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "維修單狀態回溯失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "維修單狀態回溯失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("維修單狀態回溯流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "維修單狀態回溯取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "維修單狀態回溯流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "維修單狀態回溯失敗");
        }
    }

    /// <summary>
    /// 確認維修開始，將維修單狀態改為維修中。
    /// </summary>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(MaintenanceOrderStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderStatusChangeResponse>> ConfirmMaintenanceAsync([FromBody] MaintenanceOrderConfirmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.ConfirmMaintenanceAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "確認維修失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "確認維修失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("確認維修流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "確認維修取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "確認維修流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "確認維修失敗");
        }
    }

    /// <summary>
    /// 編輯維修單資料，沿用估價單編輯的完整結構提交更新。
    /// </summary>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
          "orderNo": "O25100001",
          "store": {
            "technicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
            "source": "官方網站",
            "reservationDate": "2024-10-15T10:00:00",
            "repairDate": "2024-10-25T09:00:00"
          },
          "car": {
            "carUid": "Ca_00D20FB3-E0D1-440A-93C4-4F62AB511C2D"
          },
          "customer": {
            "customerUid": "Cu_1B65002E-EEC5-42FA-BBBB-6F5E4708610A"
          },
          "damages": {
            "dent": [
              {
                "photos": "Ph_759F19C7-5D62-4DB2-8021-2371C3136F7B",
                "position": "前保桿",
                "dentStatus": "大面積",
                "description": "需板金搭配烤漆",
                "estimatedAmount": 4500,
                "fixTypeUid": "F_DENT_SAMPLE",
                "fixType": "F_DENT_SAMPLE",
                "fixTypeName": "凹痕"
              }
            ],
            "other": [
              {
                "photos": "Ph_2B71AFAE-4F9E-4E60-9AD5-16F1C927A2C8",
                "position": "保桿內塑料件",
                "dentStatus": "拆件檢測",
                "description": "需確認內部樑是否受損",
                "estimatedAmount": 1200,
                "fixTypeUid": "F_OTHER_SAMPLE",
                "fixType": "F_OTHER_SAMPLE",
                "fixTypeName": "其他"
              }
            ]
          },
          "carBodyConfirmation": {
            "signaturePhotoUid": "Ph_D4FB9159-CD9E-473A-A3D9-0A8FDD0B76F8",
            "damageMarkers": [
              {
                "x": 0.42,
                "y": 0.63,
                "hasDent": true,
                "hasScratch": false,
                "hasPaintPeel": false,
                "remark": "主要凹痕"
              }
            ]
          },
          "maintenance": {
            "fixTypeUid": "F_9C2EDFDA-9F5A-11F0-A812-000C2990DEAF",
            "reserveCar": true,
            "applyCoating": false,
            "applyWrapping": false,
            "hasRepainted": false,
            "needToolEvaluation": true,
            "otherFee": 800,
            "roundingDiscount": 200,
            "percentageDiscount": 10,
            "discountReason": "回饋老客戶",
            "estimatedRepairDays": 1,
            "estimatedRepairHours": 6,
            "estimatedRestorationPercentage": 90,
            "suggestedPaintReason": null,
            "unrepairableReason": null,
            "remark": "請於修復後通知客戶取車"
          }
        }
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrderAsync([FromBody] UpdateMaintenanceOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            await _maintenanceOrderService.UpdateOrderAsync(request, operatorName, cancellationToken);
            return NoContent();
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "更新維修單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "更新維修單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更新維修單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "更新維修單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新維修單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "更新維修單失敗");
        }
    }

    /// <summary>
    /// 續修維修單，複製估價單與相關圖片並將原維修單標記為取消。
    /// </summary>
    [HttpPost("continue")]
    [ProducesResponseType(typeof(MaintenanceOrderContinuationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderContinuationResponse>> ContinueOrderAsync([FromBody] MaintenanceOrderContinueRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.ContinueOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "續修維修單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "續修維修單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("續修維修單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，續修維修單未建立。", "續修維修單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "續修維修單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "續修維修單失敗");
        }
    }

    /// <summary>
    /// 將維修單狀態更新為完成 (290)。
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(MaintenanceOrderStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderStatusChangeResponse>> CompleteOrderAsync([FromBody] MaintenanceOrderCompleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.CompleteOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "維修完成操作失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "維修完成失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("維修完成流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "維修完成取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "維修完成流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "維修完成失敗");
        }
    }

    /// <summary>
    /// 將維修單狀態更新為終止 (295)。
    /// </summary>
    [HttpPost("terminate")]
    [ProducesResponseType(typeof(MaintenanceOrderStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceOrderStatusChangeResponse>> TerminateOrderAsync([FromBody] MaintenanceOrderTerminateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.TerminateOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "終止維修失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "終止維修失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("終止維修流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "終止維修取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "終止維修流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "終止維修失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將例外轉換為 ProblemDetails，統一輸出格式。
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
    /// 從 JWT 權杖中取出操作人員名稱，優先使用 displayName。
    /// </summary>
    private string GetCurrentOperatorName()
    {
        var displayName = User.FindFirstValue("displayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        var nameClaim = User.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(nameClaim))
        {
            return nameClaim;
        }

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return sub;
        }

        return "UnknownUser";
    }
}
