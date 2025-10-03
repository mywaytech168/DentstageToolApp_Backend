using DentstageToolApp.Api.Quotations;
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
    /// 透過 POST 傳遞查詢條件以取得估價單列表，適合參數較多或需要 Body 傳遞時使用。
    /// </summary>
    /// <param name="request">查詢參數，與 GET 版本相同但由 Body 傳遞。</param>
    /// <param name="cancellationToken">取消權杖，供前端於離開頁面時停止查詢。</param>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "fixType": "DentRepair",
          "status": "110",
          "startDate": "2024-03-01T00:00:00",
          "endDate": "2024-03-31T23:59:59",
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
          "damages": [
            {
              "圖片": "Ph_759F19C7-5D62-4DB2-8021-2371C3136F7B",
              "位置": "保桿",
              "凹痕狀況": "大面積",
              "說明": "需板金搭配烤漆",
              "預估金額": 4500
            }
          ],
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
    /// 取得單一估價單的詳細資料，改以估價單編號作為查詢依據。
    /// </summary>
    [HttpPost("detail")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationNo": "Q25070001"
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
    /// 編輯估價單資料，更新車輛、客戶與類別備註。
    /// </summary>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "quotationUid": "QTN-20240301-0001",
          "car": {
            "licensePlate": "AAA-1234",
            "brand": "Toyota",
            "model": "Altis",
            "color": "銀",
            "remark": "已更新照片"
          },
          "customer": {
            "name": "林小華",
            "phone": "0988123456",
            "gender": "Male",
            "source": "Facebook",
            "remark": "同意估價"
          },
          "categoryRemarks": {
            "dent": "追加處理左後門凹痕",
            "paint": "等待調色"
          },
          "remark": "預計 3/8 完成",
          "damages": [
            {
              "圖片": [
                {
                  "photoUid": "Ph_40F81F71-19C4-48FF-AC6A-1CFB07B8BE9B",
                  "description": "正面照"
                }
              ],
              "位置": "後保桿",
              "凹痕狀況": "輕微凹陷",
              "說明": "需拆卸調整並搭配烤漆",
              "預估金額": 3500
            }
          ],
          "carBodyConfirmation": {
            "signaturePhotoUid": "Ph_9973BFA3-2E36-45F2-9BDE-AB1D6B7F73B1",
            "damageMarkers": [
              {
                "x": 0.35,
                "y": 0.62,
                "hasDent": true,
                "hasScratch": false,
                "hasPaintPeel": false,
                "remark": "主要受損位置"
              }
            ]
          },
          "maintenance": {
            "fixTypeUid": "F_9C2EDFDA-9F5A-11F0-A812-000C2990DEAF",
            "reserveCar": false,
            "applyCoating": false,
            "applyWrapping": false,
            "hasRepainted": false,
            "needToolEvaluation": true,
            "otherFee": 500,
            "roundingDiscount": 0,
            "percentageDiscount": 5,
            "discountReason": "老客戶回訪",
            "estimatedRepairDays": 1,
            "estimatedRepairHours": 6,
            "estimatedRestorationPercentage": 90,
            "remark": "更新備註內容"
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
