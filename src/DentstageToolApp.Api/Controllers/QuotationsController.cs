using System.Collections.Generic;
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
    /// 取得估價單列表資料，後續可擴充查詢條件或分頁參數。
    /// </summary>
    /// <param name="cancellationToken">取消權杖，供前端在切換頁面時停止查詢。</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuotationSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuotationSummaryResponse>>> GetQuotationsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("查詢估價單列表，準備呼叫服務取得資料。");

        var quotations = await _quotationService.GetQuotationsAsync(cancellationToken);

        return Ok(quotations);
    }

    // ---------- 方法區 ----------
    // 目前無額外私有方法，預留區塊供後續擴充篩選或轉換邏輯。

    // ---------- 生命週期 ----------
    // 控制器不涉及生命週期事件，保留區塊以符合專案結構規範。
}
