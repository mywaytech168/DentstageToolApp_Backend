using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Quotations;
using DentstageToolApp.Api.Services.Quotation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 估價單查詢 API，提供前台取得估價單列表的資料來源。
/// </summary>
[ApiController]
[Route("api/quotations")]
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
    [ProducesResponseType(typeof(QuotationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotationListResponse>> SearchQuotationsAsync([FromBody] QuotationListQuery request, CancellationToken cancellationToken)
    {
        // 若 Body 未帶入資料，建立預設查詢參數避免空參考例外。
        var query = request ?? new QuotationListQuery();

        _logger.LogDebug("POST 查詢估價單列表，參數：{@Query}", query);

        var quotations = await _quotationService.GetQuotationsAsync(query, cancellationToken);

        return Ok(quotations);
    }

    // ---------- 方法區 ----------
    // 目前無額外私有方法，預留區塊供後續擴充篩選或轉換邏輯。

    // ---------- 生命週期 ----------
    // 控制器不涉及生命週期事件，保留區塊以符合專案結構規範。
}
