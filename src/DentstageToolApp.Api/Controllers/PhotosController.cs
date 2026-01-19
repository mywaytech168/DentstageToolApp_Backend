using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Photos;
using DentstageToolApp.Api.Services.Photo;
using DentstageToolApp.Api.Services.Quotation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 圖片管理控制器，提供上傳與下載 PhotoUID 的端點。
/// </summary>
[ApiController]
[Route("api/photos")]
public class PhotosController : ControllerBase
{
    /// <summary>
    /// 下載圖片命名路由常數，避免各處硬編碼。
    /// </summary>
    private const string DownloadRouteName = "DownloadPhoto";

    private readonly IPhotoService _photoService;
    private readonly ILogger<PhotosController> _logger;

    /// <summary>
    /// 建構子，注入照片服務與記錄器。
    /// </summary>
    public PhotosController(IPhotoService photoService, ILogger<PhotosController> logger)
    {
        _photoService = photoService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 上傳圖片並取得 PhotoUID，後續估價單可直接引用此識別碼。
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadPhotoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadPhotoResponse>> UploadAsync([FromForm] UploadPhotoRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.File is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "缺少圖片檔案",
                Detail = "請提供要上傳的圖片檔案。",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var response = await _photoService.UploadAsync(request.File, cancellationToken);

            // 由於 CreatedAtAction 需要產生有效路由資訊，若缺少 PhotoUID 則以 200 回應避免例外。
            if (string.IsNullOrWhiteSpace(response.PhotoUid))
            {
                _logger.LogWarning("上傳圖片成功但缺少 PhotoUID，改以 200 回傳結果");
                return Ok(response);
            }

            // 透過命名路由回傳 201 Created，確保位置標頭正確並避免產生找不到路由的例外。
            return CreatedAtRoute(DownloadRouteName, new { photoUid = response.PhotoUid }, response);
        }
        catch (QuotationManagementException ex)
        {
            _logger.LogWarning(ex, "上傳圖片失敗：{Message}", ex.Message);
            return StatusCode((int)ex.StatusCode, new ProblemDetails
            {
                Title = "圖片上傳失敗",
                Detail = ex.Message,
                Status = (int)ex.StatusCode
            });
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "寫入圖片檔案時發生錯誤。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "圖片儲存失敗",
                Detail = "儲存圖片時發生錯誤，請稍後再試。",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// 依 PhotoUID 下載圖片檔案，前端可直接取得原檔供預覽或下載。
    /// </summary>
    [HttpGet("{photoUid}", Name = DownloadRouteName)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync([FromRoute] string photoUid, CancellationToken cancellationToken)
    {
        var photo = await _photoService.GetAsync(photoUid, cancellationToken);
        if (photo is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到圖片",
                Detail = "指定的圖片不存在或已被移除。",
                Status = StatusCodes.Status404NotFound
            });
        }

        return File(photo.Content, photo.ContentType, photo.FileName);
    }

    

    // ---------- 方法區 ----------
    // 控制器邏輯單純，暫無額外私有方法。

    // ---------- 生命週期 ----------
    // 控制器由 ASP.NET Core 管理生命週期，無需自行釋放資源。
}
