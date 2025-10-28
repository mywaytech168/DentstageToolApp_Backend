using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Customers;
using DentstageToolApp.Api.Services.Customer;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DentstageToolApp.Api.Models.Pagination;

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
    /// 取得所有客戶的概覽清單。
    /// </summary>
    /// <remarks>
    /// GET /api/customers?page=1&amp;pageSize=20
    /// </remarks>
    /// <param name="pagination">分頁條件，預設第一頁、每頁二十筆。</param>
    /// <param name="cancellationToken">取消權杖，供前端在需要時中止請求。</param>
    [HttpGet]
    [ProducesResponseType(typeof(CustomerListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerListResponse>> GetCustomersAsync(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        try
        {
            var paginationRequest = pagination ?? new PaginationRequest();
            var response = await _customerLookupService.GetCustomersAsync(paginationRequest, cancellationToken);
            return Ok(response);
        }
        catch (CustomerLookupException ex)
        {
            _logger.LogWarning(ex, "取得客戶列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢客戶列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("客戶列表查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢客戶列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得客戶列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢客戶列表失敗");
        }
    }

    /// <summary>
    /// 透過客戶識別碼取得完整客戶資料。
    /// </summary>
    /// <param name="customerUid">客戶識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet("{customerUid}")]
    [ProducesResponseType(typeof(CustomerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailResponse>> GetCustomerAsync(string customerUid, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _customerLookupService.GetCustomerAsync(customerUid, cancellationToken);
            return Ok(response);
        }
        catch (CustomerLookupException ex)
        {
            _logger.LogWarning(ex, "取得客戶明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢客戶資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("客戶明細查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢客戶資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得客戶明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢客戶資料失敗");
        }
    }

    /// <summary>
    /// 新增客戶資料，建立姓名、電話、來源與備註等欄位。
    /// </summary>
    /// <remarks>
    /// {"phone": "0988963537"}
    /// </remarks>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "customerName": "林小華",
          "phone": "0988123456",
          "category": "一般客戶",
          "gender": "Male",
          "county": "高雄市",
          "township": "左營區",
          "email": "demo@dentstage.com",
          "source": "Facebook",
          "reason": "想了解凹痕修復方案",
          "remark": "首次到店，請協助安排體驗"
        }
        """)]
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
    /// 編輯客戶資料，更新姓名、電話、來源與備註等欄位。
    /// </summary>
    /// <remarks>
    /// {
    ///   "customerUid": "Cu_1B65002E-EEC5-42FA-BBBB-6F5E4708610A",
    ///   "customerName": "林小華",
    ///   "phone": "0988123456",
    ///   "category": "一般客戶",
    ///   "gender": "Male",
    ///   "county": "高雄市",
    ///   "township": "左營區",
    ///   "email": "demo@dentstage.com",
    ///   "source": "Facebook",
    ///   "reason": "想了解凹痕修復方案",
    ///   "remark": "首次到店，請協助安排體驗"
    /// }
    /// </remarks>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
            "customerUid": "Cu_E1545903-EBBA-468C-B929-52028CAD98C3",
            "customerName": "林小華",
            "phone": "0988123456",
            "email": "test@gmail.com",
            "category": "一般客戶",
            "gender": "Male",
            "county": "高雄市",
            "township": "左營區",
            "source": "Facebook",
            "remark": "首次到店，請協助安排體驗",
            "createdAt": "2025-06-29T13:15:04",
            "modifiedAt": "2025-10-28T08:08:36"
        }
        """)]
    [ProducesResponseType(typeof(EditCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditCustomerResponse>> EditCustomerAsync([FromBody] EditCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // ModelState 會帶有詳細欄位錯誤，直接回傳標準 ProblemDetails。
            return ValidationProblem(ModelState);
        }

        try
        {
            // 透過 JWT 取得操作人員名稱，統一填寫修改者資訊。
            var operatorName = GetCurrentOperatorName();
            var response = await _customerManagementService.EditCustomerAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (CustomerManagementException ex)
        {
            _logger.LogWarning(ex, "編輯客戶失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯客戶資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯客戶流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯客戶資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯客戶流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯客戶資料失敗");
        }
    }

    /// <summary>
    /// 透過電話關鍵字搜尋客戶資料與維修統計資訊。
    /// </summary>
    /// <remarks>
    /// {"phone": "0988963537"}
    /// </remarks>
    [HttpPost("phone-search")]
    [SwaggerMockRequestExample(
        """
        {
          "phone": "0988123456"
        }
        """)]
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

    /// <summary>
    /// 透過電話搜尋客戶並取得估價單、維修單的完整清單。
    /// </summary>
    /// <remarks>
    /// {"phone": "0988963537"}
    /// </remarks>
    [HttpPost("customer-phone-search")]
    [SwaggerMockRequestExample(
        """
        {
          "phone": "0988123456"
        }
        """)]
    [ProducesResponseType(typeof(CustomerPhoneSearchDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerPhoneSearchDetailResponse>> SearchCustomerByPhoneWithDetailsAsync(
        [FromBody] CustomerPhoneSearchRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BuildProblemDetails(HttpStatusCode.BadRequest, "請提供欲查詢的電話號碼。", "電話搜尋失敗");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            // 回傳包含估價單與維修單的完整結果，方便後台詳查客戶歷史。
            var response = await _customerLookupService.SearchCustomerWithDetailsAsync(request, cancellationToken);
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

    /// <summary>
    /// 刪除客戶資料，刪除前會確認是否仍有報價單、工單或黑名單紀錄。
    /// </summary>
    [HttpPost("delete")]
    // 使用 SwaggerMockRequestExample 提供刪除操作的請求格式範例，降低前端串接時的欄位猜測成本。
    [SwaggerMockRequestExample(
        """
        {
          "customerUid": "Cu_5AF83218-6C72-4B2F-95A4-2B5FCB029B3B"
        }
        """)]
    [ProducesResponseType(typeof(DeleteCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteCustomerResponse>> DeleteCustomerAsync([FromBody] DeleteCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _customerManagementService.DeleteCustomerAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (CustomerManagementException ex)
        {
            _logger.LogWarning(ex, "刪除客戶失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除客戶資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除客戶流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除客戶資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除客戶流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除客戶資料失敗");
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
