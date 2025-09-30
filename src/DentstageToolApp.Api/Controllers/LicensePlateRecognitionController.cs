using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.LicensePlates;
using DentstageToolApp.Api.Services.LicensePlate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 車牌辨識控制器，提供前端上傳影像並取得車輛資訊的端點。
/// </summary>
[ApiController]
[Route("api/license-plates")]
[Authorize]
public class LicensePlateRecognitionController : ControllerBase
{
    private readonly ILicensePlateRecognitionService _licensePlateRecognitionService;
    private readonly ILogger<LicensePlateRecognitionController> _logger;

    /// <summary>
    /// 建構子，注入車牌辨識服務與記錄器。
    /// </summary>
    public LicensePlateRecognitionController(
        ILicensePlateRecognitionService licensePlateRecognitionService,
        ILogger<LicensePlateRecognitionController> logger)
    {
        _licensePlateRecognitionService = licensePlateRecognitionService;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <summary>
    /// 上傳車牌影像並回傳車牌、車輛資料與維修紀錄。
    /// </summary>
    /// <param name="request">包含影像檔案或 Base64 的請求物件。</param>
    /// <param name="cancellationToken">取消權杖，用於前端中止等待時終止作業。</param>
    [HttpPost("recognitions")]
    [ProducesResponseType(typeof(LicensePlateRecognitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicensePlateRecognitionResponse>> RecognizeAsync(
        [FromForm] LicensePlateRecognitionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if ((request.Image is null || request.Image.Length == 0) && string.IsNullOrWhiteSpace(request.ImageBase64))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "車牌辨識請求資料不完整",
                    Detail = "請提供車牌照片檔案或 Base64 影像字串。",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var imageBytes = await ConvertToBytesAsync(request.Image, request.ImageBase64, cancellationToken);
            var imageSource = new LicensePlateImageSource
            {
                FileName = request.Image?.FileName,
                ImageBytes = imageBytes
            };

            var recognition = await _licensePlateRecognitionService.RecognizeAsync(imageSource, cancellationToken);

            if (recognition is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "車牌辨識失敗",
                    Detail = "未能成功辨識車牌，請確認照片是否清晰並重新上傳。",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(recognition);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Base64 影像格式錯誤。");
            return BadRequest(new ProblemDetails
            {
                Title = "Base64 影像格式錯誤",
                Detail = "Base64 影像格式錯誤，請確認內容是否完整。",
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "影像檔案內容無效。");
            return BadRequest(new ProblemDetails
            {
                Title = "影像內容無效",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "車牌辨識服務組態錯誤。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "車牌辨識服務異常",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 依據上傳型態將影像轉換成位元組陣列，統一交給辨識服務。
    /// </summary>
    /// <param name="file">使用者上傳的檔案。</param>
    /// <param name="base64">Base64 字串。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>影像位元組陣列。</returns>
    private static async Task<byte[]> ConvertToBytesAsync(IFormFile? file, string? base64, CancellationToken cancellationToken)
    {
        if (file is not null && file.Length > 0)
        {
            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }

        if (!string.IsNullOrWhiteSpace(base64))
        {
            return Convert.FromBase64String(base64);
        }

        throw new InvalidDataException("未提供可用的影像內容，請重新上傳檔案或 Base64 字串。");
    }

    // ---------- 生命週期 ----------
    // 控制器無額外生命週期事件，保留區塊以符合檔案結構規範。
}
