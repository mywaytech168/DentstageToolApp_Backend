using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.LicensePlates;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace DentstageToolApp.Api.Services.LicensePlate;

/// <summary>
/// 使用 Tesseract OCR 實作的車牌辨識服務，負責整合影像辨識與資料庫查詢。
/// </summary>
public class LicensePlateRecognitionService : ILicensePlateRecognitionService
{
    private static readonly Regex PlateCandidateRegex = new("[A-Z0-9]{4,10}", RegexOptions.Compiled);

    private readonly TesseractOcrOptions _options;
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<LicensePlateRecognitionService> _logger;

    /// <summary>
    /// 建構子，注入 Tesseract 組態與資料庫內容類別。
    /// </summary>
    public LicensePlateRecognitionService(
        IOptions<TesseractOcrOptions> options,
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

        if (string.IsNullOrWhiteSpace(_options.TessDataPath) || !Directory.Exists(_options.TessDataPath))
        {
            throw new InvalidOperationException("Tesseract tessdata 路徑未設定或不存在，請於組態確認 TessDataPath。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- OCR 解析區 ----------
        var (rawText, confidence) = await RunTesseractAsync(imageSource.ImageBytes, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            _logger.LogWarning("Tesseract 未從影像讀取到任何文字，檔案名稱：{FileName}", imageSource.FileName);
            return null;
        }

        var candidatePlate = ExtractPlateCandidate(rawText);
        if (string.IsNullOrWhiteSpace(candidatePlate))
        {
            _logger.LogWarning("Tesseract 雖取得文字但未找到符合格式的車牌，檔案名稱：{FileName}", imageSource.FileName);
            return null;
        }

        var normalizedPlate = NormalizePlate(candidatePlate);
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            _logger.LogWarning("正規化車牌後為空值，原始候選：{Candidate}", candidatePlate);
            return null;
        }

        // ---------- 資料庫查詢區 ----------
        var car = await _dbContext.Cars
            .Include(c => c.Orders)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.CarNo == normalizedPlate
                    || c.CarNo == candidatePlate
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

    // ---------- 方法區 ----------

    /// <summary>
    /// 以 Tesseract 分析影像，回傳完整文字與信心度。
    /// </summary>
    /// <param name="imageBytes">影像的位元組陣列。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>影像中的文字與信心度百分比。</returns>
    private async Task<(string Text, double Confidence)> RunTesseractAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await Task.Run(() =>
            {
                using var engine = CreateEngine();
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);

                var text = page.GetText() ?? string.Empty;
                var meanConfidence = page.GetMeanConfidence() * 100d;

                return (text, meanConfidence);
            }, cancellationToken);
        }
        catch (TesseractException ex)
        {
            throw new InvalidOperationException("Tesseract OCR 執行失敗，請檢查 tessdata 與語系設定。", ex);
        }
    }

    /// <summary>
    /// 建立 Tesseract 引擎，套用字元白名單與版面模式設定。
    /// </summary>
    /// <returns>可用的 <see cref="TesseractEngine"/> 實例。</returns>
    private TesseractEngine CreateEngine()
    {
        var engine = new TesseractEngine(_options.TessDataPath, _options.Language, EngineMode.Default);

        if (!string.IsNullOrWhiteSpace(_options.CharacterWhitelist))
        {
            engine.SetVariable("tessedit_char_whitelist", _options.CharacterWhitelist);
        }

        if (!string.IsNullOrWhiteSpace(_options.PageSegmentationMode) && Enum.TryParse<PageSegMode>(_options.PageSegmentationMode, true, out var mode))
        {
            engine.DefaultPageSegMode = mode;
        }

        return engine;
    }

    /// <summary>
    /// 從 Tesseract 結果中挑選符合車牌格式的候選文字。
    /// </summary>
    /// <param name="rawText">Tesseract 輸出的完整文字。</param>
    /// <returns>符合格式的車牌字串。</returns>
    private static string? ExtractPlateCandidate(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var uppercaseText = rawText.ToUpperInvariant();
        var matches = PlateCandidateRegex.Matches(uppercaseText);

        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var normalized = NormalizePlate(match.Value);
            if (!string.IsNullOrWhiteSpace(normalized) && normalized.Length is >= 5 and <= 8)
            {
                return normalized;
            }
        }

        return matches.Count > 0 ? NormalizePlate(matches[0].Value) : null;
    }

    /// <summary>
    /// 將車牌號碼統一轉成大寫並移除非字母數字字元，方便比對。
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
