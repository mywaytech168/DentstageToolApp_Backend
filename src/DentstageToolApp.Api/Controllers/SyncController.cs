using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Api.Services.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [Authorize]
    public async Task<ActionResult<SyncUploadResult>> UploadAsync([FromBody] SyncUploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("請提供同步請求資料。");
        }

        var identity = ResolveTokenStoreIdentity();
        if (identity is null)
        {
            _logger.LogWarning("同步上傳缺少權杖門市資訊，拒絕請求。");
            return StatusCode(StatusCodes.Status401Unauthorized, "權杖缺少門市資訊，請重新登入後再試。");
        }

        if (!string.IsNullOrWhiteSpace(request.StoreId) && !string.Equals(request.StoreId, identity.Value.StoreId, StringComparison.Ordinal))
        {
            _logger.LogWarning("同步上傳 StoreId 與權杖不符，Token: {TokenStoreId}, Request: {RequestStoreId}", identity.Value.StoreId, request.StoreId);
            return StatusCode(StatusCodes.Status403Forbidden, "請求的 StoreId 與登入資訊不符。");
        }

        if (!string.IsNullOrWhiteSpace(request.StoreType)
            && !string.Equals(SyncServerRoles.Normalize(request.StoreType), identity.Value.StoreType, StringComparison.Ordinal))
        {
            _logger.LogWarning("同步上傳 StoreType 與權杖不符，Token: {TokenStoreType}, Request: {RequestStoreType}", identity.Value.StoreType, request.StoreType);
            return StatusCode(StatusCodes.Status403Forbidden, "請求的 StoreType 與登入資訊不符。");
        }

        // ---------- 以權杖資料覆寫請求內容，避免遭到偽造 ----------
        request.StoreId = identity.Value.StoreId;
        request.StoreType = identity.Value.StoreType;
        request.ServerRole = identity.Value.ServerRole;

        try
        {
            // ---------- 取得實際來源 IP，搭配請求內的 ServerRole 保存到同步狀態 ----------
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _syncService.ProcessUploadAsync(request, remoteIp, cancellationToken);
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
    [Authorize]
    public async Task<ActionResult<SyncDownloadResponse>> GetChangesAsync([FromQuery] SyncDownloadQuery query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            return BadRequest("請提供查詢參數。");
        }

        var identity = ResolveTokenStoreIdentity();
        if (identity is null)
        {
            _logger.LogWarning("同步下載缺少權杖門市資訊，拒絕請求。");
            return StatusCode(StatusCodes.Status401Unauthorized, "權杖缺少門市資訊，請重新登入後再試。");
        }

        if (!string.IsNullOrWhiteSpace(query.StoreId) && !string.Equals(query.StoreId, identity.Value.StoreId, StringComparison.Ordinal))
        {
            _logger.LogWarning("同步下載 StoreId 與權杖不符，Token: {TokenStoreId}, Request: {RequestStoreId}", identity.Value.StoreId, query.StoreId);
            return StatusCode(StatusCodes.Status403Forbidden, "請求的 StoreId 與登入資訊不符。");
        }

        if (!string.IsNullOrWhiteSpace(query.StoreType)
            && !string.Equals(SyncServerRoles.Normalize(query.StoreType), identity.Value.StoreType, StringComparison.Ordinal))
        {
            _logger.LogWarning("同步下載 StoreType 與權杖不符，Token: {TokenStoreType}, Request: {RequestStoreType}", identity.Value.StoreType, query.StoreType);
            return StatusCode(StatusCodes.Status403Forbidden, "請求的 StoreType 與登入資訊不符。");
        }

        // ---------- 以權杖資訊更新查詢條件，確保不會取得其他門市的資料 ----------
        query.StoreId = identity.Value.StoreId;
        query.StoreType = identity.Value.StoreType;
        query.ServerRole = identity.Value.ServerRole;

        try
        {
            // ---------- 將來源伺服器角色與 IP 傳遞給服務層，供中央更新 store_sync_states ----------
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _syncService.GetUpdatesAsync(query.StoreId, query.StoreType, query.LastSyncTime, query.PageSize, query.ServerRole, remoteIp, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "同步差異查詢參數錯誤。");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 從 JWT 權杖解析門市識別資訊，供同步流程驗證來源。
    /// </summary>
    private (string StoreId, string StoreType, string ServerRole)? ResolveTokenStoreIdentity()
    {
        var storeId = User.FindFirstValue("storeId");
        var storeType = SyncServerRoles.Normalize(User.FindFirstValue("storeType"));
        var serverRole = SyncServerRoles.Normalize(User.FindFirstValue("serverRole"));

        if (string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(storeType))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(serverRole))
        {
            // ---------- 沒有伺服器角色時，仍回傳門市資訊供紀錄使用 ----------
            serverRole = storeType;
        }

        return (storeId, storeType, serverRole);
    }
}
