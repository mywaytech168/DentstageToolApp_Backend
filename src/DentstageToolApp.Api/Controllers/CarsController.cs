using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Cars;
using DentstageToolApp.Api.Services.Car;
using DentstageToolApp.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 車輛維運 API，提供新增車輛的資料入口。
/// </summary>
[ApiController]
[Route("api/cars")]
[Authorize]
public class CarsController : ControllerBase
{
    private readonly ICarManagementService _carManagementService;
    private readonly ICarQueryService _carQueryService;
    private readonly ILogger<CarsController> _logger;

    /// <summary>
    /// 建構子，注入車輛維運服務與記錄器。
    /// </summary>
    public CarsController(
        ICarManagementService carManagementService,
        ICarQueryService carQueryService,
        ILogger<CarsController> logger)
    {
        _carManagementService = carManagementService;
        _carQueryService = carQueryService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 取得車輛列表，供前端顯示車輛資料。
    /// </summary>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet]
    [ProducesResponseType(typeof(CarListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarListResponse>> GetCarsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _carQueryService.GetCarsAsync(cancellationToken);
            return Ok(response);
        }
        catch (CarQueryServiceException ex)
        {
            _logger.LogWarning(ex, "取得車輛列表失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢車輛列表失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("車輛列表查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢車輛列表已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得車輛列表流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢車輛列表失敗");
        }
    }

    /// <summary>
    /// 透過車輛識別碼取得詳細資料。
    /// </summary>
    /// <param name="carUid">車輛識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    [HttpGet("{carUid}")]
    [ProducesResponseType(typeof(CarDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarDetailResponse>> GetCarAsync(string carUid, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _carQueryService.GetCarAsync(carUid, cancellationToken);
            return Ok(response);
        }
        catch (CarQueryServiceException ex)
        {
            _logger.LogWarning(ex, "取得車輛明細失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "查詢車輛資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("車輛明細查詢流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "查詢車輛資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得車輛明細流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "查詢車輛資料失敗");
        }
    }

    /// <summary>
    /// 新增車輛資料，建立車牌、品牌 UID、型號 UID 與備註等欄位。
    /// </summary>
    /// <remarks>
    /// 
    /// {
    ///   "carPlateNumber": "AAA-4445",
    ///   "brandUid": "B_C7CAB67F-9F5A-11F0-A812-000C2990DEAF",
    ///   "modelUid": "M_E706D04B-9F5A-11F0-A812-000C2990DEAF",
    ///   "color": "黃",
    ///   "remark": ""
    /// }
    /// 
    /// </remarks>
    [HttpPost]
    [SwaggerMockRequestExample(
        """
        {
          "carPlateNumber": "AAA-1234",
          "brandUid": "B_C7CAB67F-9F5A-11F0-A812-000C2990DEAF",
          "modelUid": "M_E706D04B-9F5A-11F0-A812-000C2990DEAF",
          "color": "白",
          "remark": "測試建立車輛資料"
        }
        """)]
    [ProducesResponseType(typeof(CreateCarResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCarResponse>> CreateCarAsync([FromBody] CreateCarRequest request, CancellationToken cancellationToken)
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
            var response = await _carManagementService.CreateCarAsync(request, operatorName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (CarManagementException ex)
        {
            _logger.LogWarning(ex, "新增車輛失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "新增車輛資料失敗");
        }
        catch (OperationCanceledException)
        {
            // 前端可能在等待時取消請求，寫入記錄後回傳 499 狀態碼資訊。
            _logger.LogInformation("新增車輛流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "新增車輛資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增車輛流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "新增車輛資料失敗");
        }
    }

    /// <summary>
    /// 編輯車輛資料，更新車牌、品牌、型號與備註等欄位。
    /// </summary>
    /// <remarks>
    /// {
    ///   "carUid": "Ca_00D20FB3-E0D1-440A-93C4-4F62AB511C2D",
    ///   "carPlateNumber": "AAA-1233",
    ///   "brandUid": "B_C7CAB67F-9F5A-11F0-A812-000C2990DEAF",
    ///   "modelUid": "M_E706D04B-9F5A-11F0-A812-000C2990DEAF",
    ///   "color": "黑",
    ///   "remark": "客戶更換為黑色烤漆"
    /// }
    /// </remarks>
    [HttpPost("edit")]
    [SwaggerMockRequestExample(
        """
        {
          "carUid": "Ca_00D20FB3-E0D1-440A-93C4-4F62AB511C2D",
          "carPlateNumber": "AAA-5678",
          "brandUid": "B_C7CAB67F-9F5A-11F0-A812-000C2990DEAF",
          "modelUid": "M_E706D04B-9F5A-11F0-A812-000C2990DEAF",
          "color": "黑",
          "remark": "客戶更換為黑色烤漆"
        }
        """)]
    [ProducesResponseType(typeof(EditCarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EditCarResponse>> EditCarAsync([FromBody] EditCarRequest request, CancellationToken cancellationToken)
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
            var response = await _carManagementService.EditCarAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (CarManagementException ex)
        {
            _logger.LogWarning(ex, "編輯車輛失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "編輯車輛資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("編輯車輛流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "編輯車輛資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "編輯車輛流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "編輯車輛資料失敗");
        }
    }

    /// <summary>
    /// 刪除車輛資料，刪除前會確認是否仍被報價單或工單使用。
    /// </summary>
    [HttpPost("delete")]
    // 透過 SwaggerMockRequestExample 呈現刪除車輛時必填的識別碼欄位，協助 Swagger 頁面產生正確範例。
    [SwaggerMockRequestExample(
        """
        {
          "carUid": "Ca_12F312C9-4E6C-4A4A-A6C2-6291281A8B44"
        }
        """)]
    [ProducesResponseType(typeof(DeleteCarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteCarResponse>> DeleteCarAsync([FromBody] DeleteCarRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var operatorName = GetCurrentOperatorName();
            var response = await _carManagementService.DeleteCarAsync(request, operatorName, cancellationToken);
            return Ok(response);
        }
        catch (CarManagementException ex)
        {
            _logger.LogWarning(ex, "刪除車輛失敗：{Message}", ex.Message);
            return BuildProblemDetails(ex.StatusCode, ex.Message, "刪除車輛資料失敗");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("刪除車輛流程被取消。");
            return BuildProblemDetails((HttpStatusCode)499, "請求已取消，資料未異動。", "刪除車輛資料已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除車輛流程發生未預期錯誤。");
            return BuildProblemDetails(HttpStatusCode.InternalServerError, "系統處理請求時發生錯誤，請稍後再試。", "刪除車輛資料失敗");
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
