using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.LicensePlates;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.Services.LicensePlate;

/// <summary>
/// 使用 OpenALPR CLI 實作的車牌辨識服務，整合資料庫查詢車輛資訊。
/// </summary>
public class LicensePlateRecognitionService : ILicensePlateRecognitionService
{
    private readonly OpenAlprOptions _options;
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<LicensePlateRecognitionService> _logger;

    /// <summary>
    /// 建構子，注入 OpenALPR 組態與資料庫內容類別。
    /// </summary>
    public LicensePlateRecognitionService(
        IOptions<OpenAlprOptions> options,
        DentstageToolAppContext dbContext,
        ILogger<LicensePlateRecognitionService> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LicensePlateRecognitionResponse?> RecognizeAsync(LicensePlateImageSource imageSource, CancellationToken cancellationToken)
    {
        // ---------- 參數檢核區 ----------
        if (imageSource is null)
        {
            throw new ArgumentNullException(nameof(imageSource), "未提供車牌影像來源，無法執行辨識。");
        }

        if (imageSource.ImageBytes is null || imageSource.ImageBytes.Length == 0)
        {
            throw new InvalidDataException("影像內容為空，請重新上傳清晰的車牌照片。");
        }

        if (string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            throw new InvalidOperationException("OpenALPR 執行檔路徑未設定，請於組態填入 ExecutablePath。");
        }

        if (!File.Exists(_options.ExecutablePath))
        {
            throw new FileNotFoundException("找不到設定的 OpenALPR 執行檔，請確認伺服器是否已安裝。", _options.ExecutablePath);
        }

        var tempDirectory = PrepareTemporaryDirectory();
        var tempFilePath = Path.Combine(tempDirectory, $"openalpr-{Guid.NewGuid():N}.jpg");

        // ---------- OpenALPR 呼叫區 ----------
        await File.WriteAllBytesAsync(tempFilePath, imageSource.ImageBytes, cancellationToken);

        try
        {
            var (rawPlate, confidence) = await ExecuteOpenAlprAsync(tempFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(rawPlate))
            {
                _logger.LogWarning("OpenALPR 未辨識出車牌，檔案名稱：{FileName}", imageSource.FileName);
                return null;
            }

            var normalizedPlate = NormalizePlate(rawPlate);
            if (string.IsNullOrWhiteSpace(normalizedPlate))
            {
                _logger.LogWarning("OpenALPR 辨識結果為空白，檔案名稱：{FileName}", imageSource.FileName);
                return null;
            }

            var formattedPlate = rawPlate.ToUpperInvariant();

            // ---------- 資料庫查詢區 ----------
            var car = await _dbContext.Cars
                .Include(c => c.Orders)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CarNo == formattedPlate
                        || c.CarNo == normalizedPlate
                        || c.CarNoQuery == normalizedPlate,
                    cancellationToken);

            // ---------- 組裝回應區 ----------
            var response = new LicensePlateRecognitionResponse
            {
                LicensePlateNumber = normalizedPlate,
                Confidence = Math.Round(confidence, 2),
                Brand = car?.Brand,
                Model = car?.Model,
                Color = car?.Color,
                HasMaintenanceHistory = car?.Orders?.Any() == true,
                Message = car is null
                    ? "資料庫無相符車輛，請確認車牌是否正確或需新增車輛資料。"
                    : "辨識成功，已回傳車輛基本資料與維修紀錄。",
            };

            return response;
        }
        finally
        {
            // ---------- 資源清理區 ----------
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "刪除暫存影像檔時發生例外：{FilePath}", tempFilePath);
            }
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 依據組態建立暫存資料夾，確保 OpenALPR 可以存取影像檔案。
    /// </summary>
    /// <returns>暫存資料夾路徑。</returns>
    private string PrepareTemporaryDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(_options.TemporaryImageDirectory)
            ? Path.GetTempPath()
            : _options.TemporaryImageDirectory;

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return directory;
    }

    /// <summary>
    /// 執行 OpenALPR CLI，解析 JSON 回傳並取得最佳候選車牌。
    /// </summary>
    /// <param name="imagePath">暫存影像檔案路徑。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>最佳候選車牌與信心度。</returns>
    private async Task<(string? Plate, double Confidence)> ExecuteOpenAlprAsync(string imagePath, CancellationToken cancellationToken)
    {
        var startInfo = BuildProcessStartInfo(imagePath);

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = false,
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("OpenALPR 程序無法啟動，請檢查執行檔權限。");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ProcessTimeoutSeconds));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException("OpenALPR 辨識逾時，請檢查伺服器效能或調整 ProcessTimeoutSeconds。");
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }

        var output = await standardOutputTask;
        var error = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("OpenALPR CLI 回傳錯誤碼 {ExitCode}，訊息：{Error}", process.ExitCode, error);
            throw new InvalidOperationException($"OpenALPR CLI 執行失敗：{error}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return (null, 0);
        }

        var (plate, confidence) = ParseRecognitionResult(output);
        return (plate, confidence);
    }

    /// <summary>
    /// 建立 OpenALPR CLI 所需的啟動參數，確保路徑與額外參數完整。
    /// </summary>
    /// <param name="imagePath">暫存影像檔路徑。</param>
    /// <returns>完成設定的 <see cref="ProcessStartInfo"/>。</returns>
    private ProcessStartInfo BuildProcessStartInfo(string imagePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-j");

        if (!string.IsNullOrWhiteSpace(_options.Country))
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(_options.Country);
        }

        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            startInfo.ArgumentList.Add("--region");
            startInfo.ArgumentList.Add(_options.Region);
        }

        if (!string.IsNullOrWhiteSpace(_options.ConfigFilePath))
        {
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(_options.ConfigFilePath);
        }

        if (!string.IsNullOrWhiteSpace(_options.RuntimeDataDirectory))
        {
            startInfo.ArgumentList.Add("--runtime-dir");
            startInfo.ArgumentList.Add(_options.RuntimeDataDirectory);
        }

        if (_options.AdditionalArguments is { Length: > 0 })
        {
            foreach (var argument in _options.AdditionalArguments)
            {
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }
        }

        startInfo.ArgumentList.Add(imagePath);

        return startInfo;
    }

    /// <summary>
    /// 將 OpenALPR CLI 回傳的 JSON 轉換為最佳候選車牌資訊。
    /// </summary>
    /// <param name="json">OpenALPR CLI 的輸出。</param>
    /// <returns>最佳候選車牌與信心度。</returns>
    private (string? Plate, double Confidence) ParseRecognitionResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
        {
            return (null, 0);
        }

        var bestPlate = default(string?);
        var bestConfidence = double.MinValue;

        foreach (var result in resultsElement.EnumerateArray())
        {
            // ---------- 候選名單解析區 ----------
            if (result.TryGetProperty("candidates", out var candidatesElement) && candidatesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in candidatesElement.EnumerateArray())
                {
                    var plate = candidate.TryGetProperty("plate", out var plateElement)
                        ? plateElement.GetString()
                        : null;

                    var confidence = candidate.TryGetProperty("confidence", out var confidenceElement)
                        ? confidenceElement.GetDouble()
                        : double.MinValue;

                    if (!string.IsNullOrWhiteSpace(plate) && confidence > bestConfidence)
                    {
                        bestPlate = plate;
                        bestConfidence = confidence;
                    }
                }
            }
            else if (result.TryGetProperty("plate", out var fallbackPlateElement))
            {
                // 若無 candidates，直接取主結果
                var plate = fallbackPlateElement.GetString();
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    bestPlate = plate;
                    bestConfidence = result.TryGetProperty("confidence", out var fallbackConfidenceElement)
                        ? fallbackConfidenceElement.GetDouble()
                        : 0d;
                }
            }
        }

        return (bestPlate, bestConfidence == double.MinValue ? 0 : bestConfidence);
    }

    /// <summary>
    /// 嘗試終止逾時的 OpenALPR 程序，避免資源佔用。
    /// </summary>
    /// <param name="process">要終止的程序。</param>
    private void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception killException)
        {
            _logger.LogWarning(killException, "終止 OpenALPR 程序時發生例外。");
        }
    }

    /// <summary>
    /// 將車牌號碼統一轉成大寫並移除空白與連字號，方便比對。
    /// </summary>
    /// <param name="plate">原始車牌號碼。</param>
    /// <returns>正規化後的車牌字串。</returns>
    private static string? NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return null;
        }

        var normalized = new string(plate
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();

        return normalized;
    }
}
