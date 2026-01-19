using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Services.Quotation;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 估價單查詢 API，提供前台取得估價單列表的資料來源。
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _quotationService;
    private readonly ILogger<QuotationsController> _logger;

    /// <summary>
    /// 建構子，注入估價單服務與記錄器。
    /// </summary>
    public QuotationsController(IQuotationService quotationService, ILogger<QuotationsController> logger)
    {
        _quotationService = quotationService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得估價單列表資料，可依維修類型、狀態、日期與關鍵字進行篩選，並支援分頁。
    /// </summary>
    /// <param name="query">查詢參數，對應前端的搜尋條件與分頁設定。</param>
    /// <param name="cancellationToken">取消權杖，供前端在切換頁面時停止查詢。</param>
    [HttpGet]
    [ProducesResponseType(typeof(QuotationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationListResponse>> GetQuotationsAsync([FromQuery] QuotationListQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("查詢估價單列表，參數：{@Query}", query);

        var quotations = await _quotationService.GetQuotationsAsync(query, cancellationToken);

        return Ok(quotations);
    }

    /// <summary>
    /// 取得兩年前（含）或更早的估價單列表。此端點會強制套用系統當前台北時間往前推兩年的 cutoff 條件。
    /// 同時提供 GET 與 POST 兩種呼叫方式（GET 以 QueryString，POST 以 Body 傳入複雜查詢）。
    /// </summary>
    [HttpGet("old")]
    [ProducesResponseType(typeof(QuotationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationListResponse>> GetOlderQuotationsAsync([FromQuery] QuotationListQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("查詢兩年前或更舊的估價單，參數：{@Query}", query);

        var resp = await _quotationService.GetOlderQuotationsAsync(query, cancellationToken);
        return Ok(resp);
    }

    [HttpPost("old")]
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "凹痕",
          "status": ["110", "115", "180", "186", "190", "195", "196", "191"],
          "startDate": "2023-10-01T00:00:00",
          "endDate": "2023-10-31T23:59:59",
          "customerKeyword": "林",
          "carPlateKeyword": "AAA",
          "page": 1,
          "pageSize": 20
        }
        """)]
    [ProducesResponseType(typeof(QuotationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationListResponse>> SearchOlderQuotationsAsync([FromBody] QuotationListQuery request, CancellationToken cancellationToken)
    {
        var query = request ?? new QuotationListQuery();
        _logger.LogDebug("POST 查詢兩年前或更舊的估價單，參數：{@Query}", query);

        var resp = await _quotationService.GetOlderQuotationsAsync(query, cancellationToken);
        return Ok(resp);
    }

    /// <summary>
    /// 透過 POST 傳遞查詢條件以取得估價單列表，適合參數較多或需要 Body 傳遞時使用。
    /// </summary>
    /// <param name="request">查詢參數，與 GET 版本相同但由 Body 傳遞。</param>
    /// <param name="cancellationToken">取消權杖，供前端於離開頁面時停止查詢。</param>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "凹痕",
          "status": ["110", "115", "180", "186", "190", "195", "196", "191"],
          "startDate": "2023-10-01T00:00:00",
          "endDate": "2025-10-31T23:59:59",
          "customerKeyword": "林",
          "carPlateKeyword": "AAA",
          "page": 1,
          "pageSize": 20
        }
        """)]
    [ProducesResponseType(typeof(QuotationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationListResponse>> SearchQuotationsAsync([FromBody] QuotationListQuery request, CancellationToken cancellationToken)
    {
        // 若 Body 未帶入資料，建立預設查詢參數避免空參考例外。
        var query = request ?? new QuotationListQuery();

        _logger.LogDebug("POST 查詢估價單列表，參數：{@Query}", query);

        var quotations = await _quotationService.GetQuotationsAsync(query, cancellationToken);

        return Ok(quotations);
    }

    /// <summary>
    /// 新增估價單，並回傳建立結果與編號資訊。
    /// </summary>
    [HttpPost("create")]
    [SwaggerMockRequestExample(
        """
        {
          "store": {
            "estimationTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
            "creatorTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027896",
            "bookMethod": "LINE 預約",
            "reservationDate": "2024-10-15T10:00:00",
            "repairDate": "2024-10-25T09:00:00",
            "isTemporaryCustomer": false
          },
          "car": {
            "carUid": "Ca_67E5D66A-DDDB-478E-B0CC-90975CABEC0E"
          },
          "customer": {
            "customerUid": "Cu_E1545903-EBBA-468C-B929-52028CAD98C3"
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
                "estimatedAmount": 4500
              }
            ],
            "beauty": [],
            "paint": [
              {
                "photo": "Ph_1F8AC157-5AC2-4E9C-9E0C-A5E8B4C8F3B0",
                "position": "右後葉子板",
                "positionOther": "其他",
                "dentStatus": "刮痕",
                "dentStatusOther": "刮痕補充說明",
                "description": "刮傷需補土烤漆",
                "estimatedAmount": 3200
              }
            ],
            "other": []
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
            "reserveCar": false,
            "applyCoating": true,
            "applyWrapping": true,
            "hasRepainted": true,
            "needToolEvaluation": false,
            "includeTax": false,
            "estimatedRepairDays": 1,
            "estimatedRepairHours": 3,
            "estimatedRestorationPercentage": 91,
            "suggestedPaintReason": null,
            "unrepairableReason": null,
            "categoryAdjustments": {
              "dent": {
                "otherFee": 300,
                "percentageDiscount": 5,
                "discountReason": "凹痕維修回饋方案"
              },
              "paint": {
                "otherFee": 500,
                "percentageDiscount": 8,
                "discountReason": "板烤周年折扣"
              },
              "other": {
                "otherFee": 800,
                "percentageDiscount": 10,
                "discountReason": "其他服務贈送方案"
              }
            },
            "remark": "請於修復後通知客戶取車"
          }
        }
        """)]
    [ProducesResponseType(typeof(CreateQuotationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateQuotationResponse>> CreateQuotationAsync([FromBody] CreateQuotationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // 若傳入參數未通過驗證，直接回傳標準 ProblemDetails 結構。
            return ValidationProblem(ModelState);
        }

        try
        {
            // 組裝目前登入者的上下文資訊，讓服務層可自動帶入使用者相關欄位。
            var operatorContext = GetCurrentOperatorContext();
            var response = await _quotationService.CreateQuotationAsync(request, operatorContext, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "新增估價單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增估價單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增估價單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未建立。", "新增估價單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增估價單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增估價單失敗");
        }
    }

    /// <summary>
    /// 取得隨機產生的估價單建立測試資料，協助前端快速帶入測試頁面內容。
    /// </summary>
    [HttpGet("create/random-test")]
    [ProducesResponseType(typeof(CreateQuotationTestPageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateQuotationTestPageResponse>> GetRandomCreateQuotationTestPageAsync(CancellationToken cancellationToken)
    {
        var response = await _quotationService.GenerateRandomQuotationTestPageAsync(cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// 取得單一估價單的詳細資料，改以估價單編號作為查詢依據。
    /// </summary>
    [HttpPost("detail")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001"
        }
        """)]
    [ProducesResponseType(typeof(QuotationDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationDetailResponse>> GetQuotationDetailAsync([FromBody] GetQuotationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var quotation = await _quotationService.GetQuotationAsync(request, cancellationToken);
            return Ok(quotation);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "取得估價單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取得估價單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得估價單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，無法取得估價單。", "取得估價單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得估價單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取得估價單失敗");
        }
    }

    /// <summary>
    /// 估價端確認維修：以 QuotationNo 為觸發點，由估價單建立維修單並將維修單狀態更新為 220（維修中）。
    /// 注意：此介面為估價端流程，若需確認既有工單請使用維修端對應的確認 API（或由前端改以維修端路徑呼叫）。
    /// </summary>
    [HttpPost("confirm-maintenance")]
        [SwaggerMockRequestExample(
                """
                {
                    "quotationNo": "Q25100001"
                }
                """)]
    [ProducesResponseType(typeof(QuotationMaintenanceConversionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationMaintenanceConversionResponse>> ConfirmMaintenanceAsync([FromBody] DentstageToolApp.Api.Models.MaintenanceOrders.MaintenanceOrQuotationConfirmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            // 目前估價端的確認流程僅以 QuotationNo 為觸發點：由估價單建立並進入維修中 (220)。
            if (string.IsNullOrWhiteSpace(request?.QuotationNo))
            {
                throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供 QuotationNo 以確認維修並建立維修單。");
            }

            var convReq = new QuotationMaintenanceRequest { QuotationNo = request.QuotationNo };
            var convResp = await _quotationService.ConvertToMaintenanceAsync(convReq, operatorName, cancellationToken);
            return Ok(convResp);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "估價端確認維修失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "估價端確認維修失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("估價端確認維修流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，維修單未更新。", "估價端確認維修取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "估價端確認維修發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "估價端確認維修失敗");
        }
    }

    /// <summary>
    /// 編輯估價單資料，更新車輛、客戶與類別備註。
    /// </summary>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
          "store": {
            "estimationTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
            "creatorTechnicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
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
                "estimatedAmount": 4500
              }
            ],
            "beauty": [
              {
                "photo": "Ph_A67C6B52-A09F-4C7D-B1F1-9CDA3B67E2C5",
                "position": "內裝皮革",
                "positionOther": "其他",
                "dentStatus": "美容拋光",
                "dentStatusOther": "拋光補充說明",
                "description": "座椅刮痕需要美容處理",
                "estimatedAmount": 1500
              }
            ],
            "paint": [],
            "other": []
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
            "reserveCar": true,
            "applyCoating": false,
            "applyWrapping": false,
            "hasRepainted": false,
            "needToolEvaluation": true,
            "includeTax": false,
            "estimatedRepairDays": 1,
            "estimatedRepairHours": 6,
            "estimatedRestorationPercentage": 90,
            "suggestedPaintReason": null,
            "unrepairableReason": null,
            "roundingDiscount": 200,
            "categoryAdjustments": {
              "dent": {
                "otherFee": 300,
                "percentageDiscount": 5,
                "discountReason": "凹痕維修回饋方案"
              },
              "paint": {
                "otherFee": 500,
                "percentageDiscount": 8,
                "discountReason": "板烤周年折扣"
              },
              "other": {
                "otherFee": 800,
                "percentageDiscount": 10,
                "discountReason": "其他服務贈送方案"
              }
            },
            "remark": "請於修復後通知客戶取車"
          }
        }
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuotationAsync([FromBody] UpdateQuotationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = GetCurrentOperatorName();
            await _quotationService.UpdateQuotationAsync(request, operatorName, cancellationToken);
            return NoContent();
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "更新估價單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "更新估價單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更新估價單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "更新估價單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新估價單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "更新估價單失敗");
        }
    }

    /// <summary>
    /// 將估價單狀態更新為估價完成 (180)。
    /// </summary>
    [HttpPost("evaluate")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001"
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> CompleteEvaluationAsync([FromBody] QuotationEvaluateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 從權杖解析目前操作人員名稱，供服務層記錄狀態異動。
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.CompleteEvaluationAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "估價完成失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "估價完成失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("估價完成流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "估價完成取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "估價完成流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "估價完成失敗");
        }
    }

    /// <summary>
    /// 取消估價單，將狀態改為 195 並記錄操作時間。
    /// </summary>
    [HttpPost("cancel")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
          "reason": "客戶臨時改期"
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> CancelQuotationAsync([FromBody] QuotationCancelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.CancelQuotationAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "取消估價單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取消估價單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取消估價單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "取消估價單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消估價單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取消估價單失敗");
        }
    }

    /// <summary>
    /// 刪除估價單，僅允許「估價中 / 編輯中」狀態，並檢查是否已建立工單。
    /// </summary>
    [HttpPost("delete")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001"
        }
        """)]
    [ProducesResponseType(typeof(DeleteQuotationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteQuotationResponse>> DeleteQuotationAsync([FromBody] DeleteQuotationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.DeleteQuotationAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "刪除估價單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除估價單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除估價單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未刪除。", "刪除估價單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除估價單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除估價單失敗");
        }
    }

    /// <summary>
    /// 將估價單轉為預約，寫入預約日期後回傳狀態資訊。
    /// </summary>
    [HttpPost("reserve")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
                    "reservationDate": "2024-11-20T10:00:00",
                    "reservationContent": "前保桿板烤，請預留代步車"
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> ConvertToReservationAsync([FromBody] QuotationReservationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.ConvertToReservationAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "轉預約失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "轉預約失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("轉預約流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "轉預約取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "轉預約流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "轉預約失敗");
        }
    }

    /// <summary>
    /// 更改既有預約日期，維持狀態為 190。
    /// </summary>
    [HttpPost("reserve/update")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
                    "reservationDate": "2024-11-25T14:30:00",
                    "reservationContent": "調整到下午時段，仍需代步車"
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> UpdateReservationDateAsync([FromBody] QuotationReservationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.UpdateReservationDateAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "更改預約失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "更改預約失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更改預約流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "更改預約取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更改預約流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "更改預約失敗");
        }
    }

    /// <summary>
    /// 取消既有預約，狀態改為 195 並清除預約日期。
    /// </summary>
    [HttpPost("reserve/cancel")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001",
          "reason": "客戶無法到場",
          "clearReservation": true
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> CancelReservationAsync([FromBody] QuotationCancelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.CancelReservationAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "取消預約失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "取消預約失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取消預約流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "取消預約取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消預約流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "取消預約失敗");
        }
    }

    /// <summary>
    /// 將估價單狀態回朔至上一個有效狀態。
    /// </summary>
    [HttpPost("revert")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001"
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> RevertReservationStatusAsync([FromBody] QuotationRevertStatusRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.RevertQuotationStatusAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "狀態回溯失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "狀態回溯失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("狀態回溯流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "狀態回溯取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "狀態回溯流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "狀態回溯失敗");
        }
    }

    /// <summary>
    /// 僅標記為待維修（191），不建立維修單
    /// </summary>
    [HttpPost("maintenance")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001"
        }
        """)]
    [ProducesResponseType(typeof(QuotationStatusChangeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationStatusChangeResponse>> ConvertToMaintenanceAsync([FromBody] QuotationMaintenanceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            // 新流程：POST /api/quotations/maintenance 僅標記為待維修（191），不建立維修單
            var response = await _quotationService.MarkQuotationWaitingAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "轉維修失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "轉維修失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("轉維修流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，估價單未更新。", "轉維修取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "轉維修流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "轉維修失敗");
        }
    }

    /// <summary>
    /// 複製指定的估價單（包含所有照片），建立新的估價單，狀態設為 110（估價中）。
    /// </summary>
    [HttpPost("duplicate")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25100001"
        }
        """)]
    [ProducesResponseType(typeof(DuplicateQuotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DuplicateQuotationResponse>> DuplicateQuotationAsync([FromBody] QuotationActionRequestBase request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var quotationNo = request.EnsureAndGetQuotationNo();
            var operatorName = GetCurrentOperatorName();
            var response = await _quotationService.DuplicateQuotationAsync(quotationNo, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "複製估價單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "複製估價單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("複製估價單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，複製未建立。", "複製估價單取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "複製估價單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "複製估價單失敗");
        }
    }

    // ---------- 方法區 ----------
    /// <summary>
    /// 將例外轉換為 ProblemDetails，統一錯誤輸出格式。
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
    /// 組合目前登入者的估價單操作上下文，包含名稱與唯一識別碼。
    /// </summary>
    private QuotationOperatorContext GetCurrentOperatorContext()
    {
        return new QuotationOperatorContext
        {
            OperatorName = GetCurrentOperatorName(),
            UserUid = GetCurrentUserUid(),
            StoreUid = GetCurrentStoreUid()
        };
    }

    /// <summary>
    /// 從 JWT 權杖中取得操作人員名稱，優先顯示名稱再回退至識別碼。
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

    /// <summary>
    /// 取得目前登入者的唯一識別碼，供建立估價單時寫入資料庫。
    /// </summary>
    private string? GetCurrentUserUid()
    {
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
    /// 從 JWT 權杖中取出門市識別碼，支援多種常見的 Claim Key 寫法。
    /// </summary>
    private string? GetCurrentStoreUid()
    {
        // 依序檢查常見欄位名稱，確保不同登入來源皆可正確回傳門市 UID。
        var claimKeys = new[] { "storeUid", "StoreUid", "storeUID", "StoreUID", "storeId", "StoreId" };
        foreach (var key in claimKeys)
        {
            var value = User.FindFirstValue(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    // ---------- 生命週期 ----------
    // 控制器不涉及生命週期事件，保留區塊以符合專案結構規範。
}
