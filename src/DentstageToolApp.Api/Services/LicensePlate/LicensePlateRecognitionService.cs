using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.LicensePlates;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAlprNet;

namespace DentstageToolApp.Api.Services.LicensePlate;

/// <summary>
/// 使用 OpenALPR 實作的車牌辨識服務，串接資料庫取得車輛資訊。
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

        if (string.IsNullOrWhiteSpace(_options.ConfigFilePath) || string.IsNullOrWhiteSpace(_options.RuntimeDataDirectory))
        {
            throw new InvalidOperationException("OpenALPR 組態未完整設定，請確認 ConfigFilePath 與 RuntimeDataDirectory。");
        }

        // ---------- OpenALPR 呼叫區 ----------
        using var openAlpr = new OpenAlpr(_options.Country, _options.ConfigFilePath, _options.RuntimeDataDirectory);
        if (!openAlpr.IsLoaded())
        {
            _logger.LogError("OpenALPR 初始化失敗，請檢查組態：{@Options}", _options);
            throw new InvalidOperationException("OpenALPR 初始化失敗，請檢查伺服器的模型與權限設定。");
        }

        var recognitionResult = openAlpr.Recognize(imageSource.ImageBytes);
        if (recognitionResult?.Results == null || recognitionResult.Results.Count == 0)
        {
            _logger.LogWarning("OpenALPR 未辨識出車牌，檔案名稱：{FileName}", imageSource.FileName);
            return null;
        }

        var bestPlate = recognitionResult.Results
            .SelectMany(result => result.Candidates)
            .OrderByDescending(candidate => candidate.Confidence)
            .FirstOrDefault();

        if (bestPlate is null)
        {
            _logger.LogWarning("OpenALPR 無可用候選車牌，檔案名稱：{FileName}", imageSource.FileName);
            return null;
        }

        var normalizedPlate = NormalizePlate(bestPlate.Plate);
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            _logger.LogWarning("OpenALPR 辨識結果為空白，檔案名稱：{FileName}", imageSource.FileName);
            return null;
        }

        var formattedPlate = bestPlate.Plate?.ToUpperInvariant();

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
            Confidence = Math.Round(bestPlate.Confidence, 2),
            Brand = car?.Brand,
            Model = car?.Model,
            Color = car?.Color,
            HasMaintenanceHistory = car?.Orders?.Any() == true,
            Message = car is null
                ? "資料庫無相符車輛，請確認車牌是否正確或需新增車輛資料。"
                : "辨識成功，已回傳車輛基本資料與維修紀錄。"
        };

        return response;
    }

    // ---------- 方法區 ----------

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
