using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Api.Services.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 提供門市資料與中央伺服器之間的同步 API。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    /// <summary>
    /// 建構子，注入同步服務與記錄器。
    /// </summary>
    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// 分店上傳差異資料。
    /// </summary>
    [HttpPost("upload")]
    [AllowAnonymous]
    public async Task<ActionResult<SyncUploadResult>> UploadAsync([FromBody] SyncUploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("請提供同步請求資料。");
        }

        try
        {
            var result = await _syncService.ProcessUploadAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "同步上傳請求參數錯誤。");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 取得中央伺服器的最新差異資料。
    /// </summary>
    [HttpGet("changes")]
    [AllowAnonymous]
    public async Task<ActionResult<SyncDownloadResponse>> GetChangesAsync([FromQuery] SyncDownloadQuery query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            return BadRequest("請提供查詢參數。");
        }

        try
        {
            var response = await _syncService.GetUpdatesAsync(query.StoreId, query.LastSyncTime, query.PageSize, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "同步差異查詢參數錯誤。");
            return BadRequest(ex.Message);
        }
    }
}
