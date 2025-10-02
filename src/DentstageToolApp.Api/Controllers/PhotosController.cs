using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Photos;
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
[Authorize]
public class PhotosController : ControllerBase
{
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
            var response = await _photoService.UploadAsync(request.File, request.Remark, cancellationToken);
            return CreatedAtAction(nameof(DownloadAsync), new { photoUid = response.PhotoUid }, response);
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
    [HttpGet("{photoUid}")]
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
