using System;
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
/// 採購單維運 API，提供查詢與維護採購單的端點。
/// </summary>
[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ILogger<PurchaseOrdersController> _logger;

    /// <summary>
    /// 建構子，注入採購服務與記錄器。
    /// </summary>
    public PurchaseOrdersController(IPurchaseService purchaseService, ILogger<PurchaseOrdersController> logger)
    {
        _purchaseService = purchaseService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得採購單列表，改以 POST 直接命中控制器根路徑並於 Body 傳遞查詢條件，支援店鋪關鍵字與日期區間搜尋。
    /// </summary>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "storeKeyword": "民權",
          "startDate": "2024-07-01",
          "endDate": "2024-07-31",
          "page": 1,
          "pageSize": 20
        }
        """)]
    [ProducesResponseType(typeof(PurchaseOrderListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PurchaseOrderListResponse>> GetPurchaseOrdersAsync([FromBody] PurchaseOrderListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 將查詢參數傳入服務層，由服務層統一處理分頁邏輯。
            var response = await _purchaseService.GetPurchaseOrdersAsync(query, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "取得採購單列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢採購單列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得採購單列表流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢採購單列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得採購單列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢採購單列表失敗");
        }
    }

    /// <summary>
    /// 取得單筆採購單明細，改由 POST 於 Body 內提供採購單單號，避免於網址上暴露識別資訊。
    /// </summary>
    [HttpPost("detail")]
    [SwaggerMockRequestExample(
        """
        {
          "purchaseOrderNo": "PO_2025070001"
        }
        """)]
    [ProducesResponseType(typeof(PurchaseOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> GetPurchaseOrderAsync([FromBody] PurchaseOrderDetailRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // 若 Body 未帶入必要欄位，立即回傳 400 讓前端修正請求格式。
            return ValidationProblem(ModelState);
        }

        try
        {
            // 將請求中的單號轉交服務層查詢，統一管理資料存取流程。
            var response = await _purchaseService.GetPurchaseOrderAsync(request.PurchaseOrderNo!, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "取得採購單明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢採購單資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("取得採購單明細流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未更新。", "查詢採購單資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得採購單明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢採購單資料失敗");
        }
    }

    /// <summary>
    /// 新增採購單，改掛載於 /create 子路徑，系統會自動以建立時間的日期填入採購日期，門市識別碼改由登入者權杖提供。
    /// </summary>
    [HttpPost("create")]
    [SwaggerMockRequestExample(
        """
        {
          "items": [
            {
              "itemName": "烤漆材料",
              "categoryUid": "PC_4B6F1F85-6C8A-4A0D-9123-5FD85E0D4C5F",
              "unitPrice": 1200,
              "quantity": 3
            },
            {
              "itemName": "黏土耗材",
              "categoryUid": null,
              "unitPrice": 350,
              "quantity": 5
            }
          ]
        }
        """)]
    [ProducesResponseType(typeof(PurchaseOrderDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> CreatePurchaseOrderAsync([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var storeUid = GetCurrentStoreUid();
            if (string.IsNullOrWhiteSpace(storeUid))
            {
                // 若權杖未提供門市識別碼，視為目前登入者尚未綁定門市，直接回傳錯誤。
                return BuildProblemDetails(HttpStatusCode.Forbidden, "目前登入者未綁定門市資訊，無法建立採購單。", "新增採購單失敗");
            }

            var ensuredStoreUid = storeUid!;
            var response = await _purchaseService.CreatePurchaseOrderAsync(request, operatorName, ensuredStoreUid, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "新增採購單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增採購單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("新增採購單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增採購單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增採購單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增採購單失敗");
        }
    }

    /// <summary>
    /// 更新採購單，請於 Request Body 內帶入欲更新的 purchaseOrderUid，若要調整門市需提供新的 storeUid。
    /// </summary>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "purchaseOrderUid": "PU_9D5F5241-6680-4EEB-A3D3-ACCCFD0B8C74",
          "storeUid": "ST_0123456789",
          "purchaseDate": "2024-07-12",
          "items": [
            {
              "purchaseItemUid": "PI_CBEA8E21-8FF7-4FC1-A4B0-54C5AC5F1ED0",
              "itemName": "烤漆材料",
              "categoryUid": "PC_4B6F1F85-6C8A-4A0D-9123-5FD85E0D4C5F",
              "unitPrice": 1250,
              "quantity": 4
            },
            {
              "itemName": "拋光劑",
              "categoryUid": "PC_93AF2D1C-2E97-4BF3-8B44-7C698D5100B7",
              "unitPrice": 450,
              "quantity": 2
            }
          ]
        }
        """)]
    [ProducesResponseType(typeof(PurchaseOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> UpdatePurchaseOrderAsync([FromBody] UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _purchaseService.UpdatePurchaseOrderAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "更新採購單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "更新採購單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更新採購單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "更新採購單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新採購單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "更新採購單失敗");
        }
    }

    /// <summary>
    /// 刪除採購單，需於 Body 同時提供 purchaseOrderUid 與 purchaseOrderNo 以雙重驗證刪除目標。
    /// </summary>
    [HttpDelete]
    [SwaggerMockRequestExample(
        """
        {
          "purchaseOrderUid": "PU_25C6F955-6CD6-4B5B-9EF4-5EAC0F0A1CC1",
          "purchaseOrderNo": "PO_2025070001"
        }
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePurchaseOrderAsync([FromBody] DeletePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            await _purchaseService.DeletePurchaseOrderAsync(request, operatorName, cancellationToken);
            return NoContent();
        }
        catch (PurchaseServiceException ex)
        {
            _logger.LogWarning(ex, "刪除採購單失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除採購單失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除採購單流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除採購單已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除採購單流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除採購單失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 統一組裝 ProblemDetails 物件，提供一致的錯誤回應格式。
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
    /// 取得操作人員名稱，優先使用 displayName，再回退到使用者識別碼。
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
    /// 取得目前登入者對應的門市識別碼（StoreUID），供建立採購單時自動帶入門市資訊。
    /// </summary>
    private string? GetCurrentStoreUid()
    {
        // 依序檢查常見 Claim Key 寫法，確保不同登入來源都能正確取得門市識別碼。
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
    // 控制器目前無需額外生命週期處理，預留區塊以符合專案規範。
}
