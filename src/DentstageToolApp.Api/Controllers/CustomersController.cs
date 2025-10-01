using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Customers;
using DentstageToolApp.Api.Services.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 客戶維運 API，提供新增客戶的資料入口。
/// </summary>
[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerManagementService _customerManagementService;
    private readonly ICustomerLookupService _customerLookupService;
    private readonly ILogger<CustomersController> _logger;

    /// <summary>
    /// 建構子，注入客戶維運服務與記錄器。
    /// </summary>
    public CustomersController(
        ICustomerManagementService customerManagementService,
        ICustomerLookupService customerLookupService,
        ILogger<CustomersController> logger)
    {
        _customerManagementService = customerManagementService;
        _customerLookupService = customerLookupService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 新增客戶資料，建立姓名、電話、來源與備註等欄位。
    /// </summary>
    /// <remarks>
    /// {"phone": "0988963537"}
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CreateCustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCustomerResponse>> CreateCustomerAsync([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // ModelState 會帶有詳細欄位錯誤，直接回傳標準 ProblemDetails。
            return ValidationProblem(ModelState);
        }

        try
        {
            // 透過 JWT 取得操作人員名稱，統一填寫建立與修改者資訊。
            var operatorName = GetCurrentOperatorName();
            var response = await _customerManagementService.CreateCustomerAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (CustomerManagementException ex)
        {
            _logger.LogWarning(ex, "新增客戶失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增客戶資料失敗");
        }
        catch (OperationCanceledException)
        {
            // 前端可能在等待時取消請求，寫入記錄後回傳 499 狀態碼資訊。
            _logger.LogInformation("新增客戶流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增客戶資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增客戶流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增客戶資料失敗");
        }
    }

    /// <summary>
    /// 透過電話關鍵字搜尋客戶資料與維修統計資訊。
    /// </summary>
    /// <remarks>
    /// {"phone": "0988963537"}
    /// </remarks>
    [HttpPost("phone-search")]
    [ProducesResponseType(typeof(CustomerPhoneSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerPhoneSearchResponse>> SearchCustomerByPhoneAsync([FromBody] CustomerPhoneSearchRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            // 若前端未傳入內容，直接回覆提示訊息避免 NullReference。
            return BuildProblemDetails(HttpStatusCode.BadRequest, "請提供欲查詢的電話號碼。", "電話搜尋失敗");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 呼叫查詢服務並回傳統一格式的結果給前端。
            var response = await _customerLookupService.SearchByPhoneAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (CustomerLookupException ex)
        {
            _logger.LogWarning(ex, "電話搜尋失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "電話搜尋失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("電話搜尋流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "電話搜尋已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "電話搜尋流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "電話搜尋失敗");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將例外轉換成 ProblemDetails，統一錯誤輸出格式。
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
    /// 從 JWT 權杖中取得操作人員名稱，優先使用顯示名稱，再回退至使用者識別碼。
    /// </summary>
    private string GetCurrentOperatorName()
    {
        // 先取 displayName Claim，確保記錄可讀性。
        var displayName = User.FindFirstValue("displayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        // 若無顯示名稱則改用 Sub 或 UniqueName 以確保可追蹤性。
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

        // 最後使用通用 NameIdentifier，避免回傳空字串造成資料庫寫入失敗。
        userUid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userUid) ? "UnknownUser" : userUid;
    }

    // ---------- 生命週期 ----------
    // 控制器目前沒有額外生命週期事件，保留區塊以符合專案規範。
}
