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
    private readonly DentstageToolApp.Api.Services.Quotation.IQuotationService _quotationService;
    private readonly ILogger<MaintenanceOrdersController> _logger;

    /// <summary>
    /// 建構子，注入維修單服務與記錄器。
    /// </summary>
    public MaintenanceOrdersController(IMaintenanceOrderService maintenanceOrderService, DentstageToolApp.Api.Services.Quotation.IQuotationService quotationService, ILogger<MaintenanceOrdersController> logger)
    {
        _maintenanceOrderService = maintenanceOrderService;
        _quotationService = quotationService;
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
    /// 取得兩年前（含）或更早的維修單列表。會強制套用系統當前台北時間往前推兩年的 cutoff 條件。
    /// 提供 GET 與 POST 兩種呼叫方式。
    /// </summary>
    [HttpGet("old")]
    [ProducesResponseType(typeof(MaintenanceOrderListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOrderListResponse>> GetOlderOrdersAsync([FromQuery] MaintenanceOrderListQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("查詢兩年前或更舊的維修單，參數：{@Query}", query);

    var resp = await _maintenanceOrderService.GetOlderOrdersAsync(query, cancellationToken);
        return Ok(resp);
    }

    [HttpPost("old")]
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "凹痕",
          "status": ["220", "295", "296", "290"],
          "startDate": "2023-10-01T00:00:00",
          "endDate": "2023-10-31T23:59:59",
          "customerKeyword": "林",
          "carPlateKeyword": "AAA",
          "page": 1,
          "pageSize": 20
        }
        """)]
    [ProducesResponseType(typeof(MaintenanceOrderListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOrderListResponse>> SearchOlderOrdersAsync([FromBody] MaintenanceOrderListQuery request, CancellationToken cancellationToken)
    {
        var query = request ?? new MaintenanceOrderListQuery();
        _logger.LogDebug("POST 查詢兩年前或更舊的維修單，參數：{@Query}", query);

        var resp = await _maintenanceOrderService.GetOlderOrdersAsync(query, cancellationToken);
        return Ok(resp);
    }

    /// <summary>
    /// 透過 POST 傳遞查詢條件取得維修單列表。
    /// </summary>
    [HttpPost]
    // 透過 SwaggerMockRequestExample 提供查詢範本，協助前端掌握可用的篩選欄位。
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "凹痕",
          "status": ["220", "295", "296", "290"],
          "startDate": "2023-10-01T00:00:00",
          "endDate": "2025-10-31T23:59:59",
          "customerKeyword": "林",
          "carPlateKeyword": "AAA",
          "page": 1,
          "pageSize": 20
        }
        """)]
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
    // 使用 Swagger 範例示範如何傳入維修單編號查詢詳細內容。
    [SwaggerMockRequestExample(
        """
        {
          "orderNo": "O25100001"
        }
        """)]
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
    // 透過 Swagger 範例提示誤按回溯操作只需傳入維修單編號。
    [SwaggerMockRequestExample(
        """
        {
          "orderNo": "O25100001"
        }
        """)]
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
    /// 編輯維修單資料，沿用估價單編輯的完整結構提交更新。
    /// </summary>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
          "orderNo": "O25100001",
          "store": {
            "estimationTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
            "creatorTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
            "serviceTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
            "source": "官方網站",
            "bookMethod": "LINE 預約",
            "reservationDate": "2024-10-15T10:00:00",
            "repairDate": "2024-10-25T09:00:00",
            "isTemporaryCustomer": false
          },
          "car": {
            "carUid": "Ca_00D20FB3-E0D1-440A-93C4-4F62AB511C2D"
          },
          "customer": {
            "customerUid": "Cu_1B65002E-EEC5-42FA-BBBB-6F5E4708610A"
          },
          "photos": {
            "dent": [
              {
                "photo": "Ph_759F19C7-5D62-4DB2-8021-2371C3136F7B",
                "position": "前保桿",
                "positionOther": "其他",
                "dentStatus": "大面積",
                "dentStatusOther": "其他凹痕描述",
                "description": "需板金搭配烤漆",
                "estimatedAmount": 4500,
                "MaintenanceProgress": 80,
                "actualAmount": 4500,
                "afterPhotoUid": "Ph_A0481C86-8F01-4BE7-9BC2-1E8EAA1C47A1"
              }
            ],
            "beauty": [],
            "paint": [],
            "other": [
              {
                "photo": "Ph_2B71AFAE-4F9E-4E60-9AD5-16F1C927A2C8",
                "position": "保桿內塑料件",
                "positionOther": "其他",
                "dentStatus": "拆件檢測",
                "dentStatusOther": "拆件補充說明",
                "description": "需確認內部樑是否受損",
                "estimatedAmount": 1200,
                "MaintenanceProgress": 50,
                "actualAmount": 1200,
                "afterPhotoUid": "Ph_BB9C7AB2-62A4-4C11-A6AE-7E20A4E1F9F2"
              }
            ]
          },
          "carBodyConfirmation": {
            "signaturePhotoUid": "Ph_D4FB9159-CD9E-473A-A3D9-0A8FDD0B76F8",
            "damageMarkers": [
              {
                "start": { "x": 0.42, "y": 0.63 },
                "end": { "x": 0.58, "y": 0.68 },
                "hasDent": true,
                "hasScratch": false,
                "hasPaintPeel": false,
                "hasScuff": false,
                "remark": "主要凹痕"
              }
            ]
          },
          "maintenance": {
            "fixType": "其他",
            "reserveCar": true,
            "applyCoating": false,
            "applyWrapping": false,
            "hasRepainted": false,
            "needToolEvaluation": true,
            "includeTax": false,
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
    /// 續修維修單，複製估價單與相關項目(維修狀態為0)。
    /// </summary>
    [HttpPost("continue")]
    // 範例展示續修操作只需提供原維修單編號，Swagger 可直接複製使用。
    [SwaggerMockRequestExample(
        """
        {
          "orderNo": "O25100001"
        }
        """)]
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
    // 提供維修完成操作的 Swagger 範例，明確傳達僅需維修單編號。
    [SwaggerMockRequestExample(
        """
        {
          "orderNo": "O25100001"
        }
        """)]
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
    // 於 Swagger 呈現終止維修的請求格式，協助串接人員理解欄位需求。
    [SwaggerMockRequestExample(
        """
        {
          "orderNo": "O25100001"
        }
        """)]
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

    /// <summary>
    /// 針對已完成的維修單進行退傭，需通過密碼驗證。
    /// </summary>
    [HttpPost("rebate")]
    // 於 Swagger 提供退傭請求範例，明確列出密碼與退傭金額欄位。
    [SwaggerMockRequestExample(
        """
        {
          "orderNo": "O25100001",
          "password": "123456",
          "rebateAmount": 100
        }
        """)]
    [ProducesResponseType(typeof(MaintenanceOrderRebateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceOrderRebateResponse>> ApplyRebateAsync([FromBody] MaintenanceOrderRebateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _maintenanceOrderService.ApplyRebateAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (MaintenanceOrderManagementException ex)
        {
            _logger.LogWarning(ex, "退傭處理失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "退傭處理失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("退傭流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，退傭未完成。", "退傭處理取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退傭流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "退傭處理失敗");
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
