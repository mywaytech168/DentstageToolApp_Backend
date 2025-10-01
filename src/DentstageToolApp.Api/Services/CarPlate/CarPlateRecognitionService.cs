using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.CarPlates;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace DentstageToolApp.Api.Services.CarPlate;

/// <summary>
/// 使用 Tesseract OCR 實作的車牌辨識服務，負責整合影像辨識與資料庫查詢。
/// </summary>
public class CarPlateRecognitionService : ICarPlateRecognitionService
{
    private static readonly Regex PlateCandidateRegex = new("[A-Z0-9]{4,10}", RegexOptions.Compiled);

    private readonly TesseractOcrOptions _options;
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CarPlateRecognitionService> _logger;

    /// <summary>
    /// 建構子，注入 Tesseract 組態與資料庫內容類別。
    /// </summary>
    public CarPlateRecognitionService(
        IOptions<TesseractOcrOptions> options,
        DentstageToolAppContext dbContext,
        ILogger<CarPlateRecognitionService> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CarPlateRecognitionResponse?> RecognizeAsync(CarPlateImageSource imageSource, CancellationToken cancellationToken)
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
        var response = new CarPlateRecognitionResponse
        {
            CarPlateNumber = normalizedPlate,
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

    /// <inheritdoc />
    public async Task<CarPlateMaintenanceHistoryResponse> GetMaintenanceHistoryAsync(string carPlateNumber, CancellationToken cancellationToken)
    {
        // ---------- 參數檢核區 ----------
        if (string.IsNullOrWhiteSpace(carPlateNumber))
        {
            throw new ArgumentException("車牌號碼不可為空，請輸入欲查詢的車牌號碼。", nameof(carPlateNumber));
        }

        var trimmedPlate = carPlateNumber.Trim();
        var normalizedPlate = NormalizePlate(trimmedPlate);

        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            throw new InvalidDataException("車牌號碼格式不正確，請確認僅輸入英數字組成的車牌。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料庫查詢區 ----------
        var car = await _dbContext.Cars
            .Include(c => c.Orders)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.CarNo == normalizedPlate
                    || c.CarNo == trimmedPlate
                    || c.CarNoQuery == normalizedPlate
                    || c.CarNoQuery == trimmedPlate,
                cancellationToken);

        var orders = new List<Order>();

        if (car?.Orders is { Count: > 0 })
        {
            // 將車輛底下的工單加入集合，作為初始資料。
            orders.AddRange(car.Orders);
        }

        var additionalOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o =>
                o.CarNo == normalizedPlate
                || o.CarNo == trimmedPlate
                || o.CarNoInput == normalizedPlate
                || o.CarNoInput == trimmedPlate
                || o.CarNoInputGlobal == trimmedPlate)
            .ToListAsync(cancellationToken);

        foreach (var order in additionalOrders)
        {
            if (orders.Any(existing => existing.OrderUid == order.OrderUid))
            {
                continue;
            }

            orders.Add(order);
        }

        // ---------- 組裝回應區 ----------
        var recordItems = orders
            .Select(order => new
            {
                Record = MapToMaintenanceRecord(order),
                SortKey = ResolveRecordDate(order)
            })
            .OrderByDescending(item => item.SortKey ?? DateTime.MinValue)
            .ThenByDescending(item => item.Record.CreatedAt ?? DateTime.MinValue)
            .Select(item => item.Record)
            .ToList();

        if (recordItems.Count == 0)
        {
            _logger.LogInformation("查無車牌 {LicensePlate} 的維修紀錄。", normalizedPlate);
        }
        else
        {
            _logger.LogInformation("查詢車牌 {LicensePlate} 的維修紀錄共 {Count} 筆。", normalizedPlate, recordItems.Count);
        }

        var referenceOrder = orders.FirstOrDefault();

        var response = new CarPlateMaintenanceHistoryResponse
        {
            CarPlateNumber = normalizedPlate,
            Brand = car?.Brand ?? referenceOrder?.Brand,
            Model = car?.Model ?? referenceOrder?.Model,
            Color = car?.Color ?? referenceOrder?.Color,
            HasMaintenanceRecords = recordItems.Count > 0,
            Records = recordItems,
            Message = recordItems.Count > 0
                ? "查詢成功，已列出歷史維修資料。"
                : "查無維修紀錄，請確認車牌是否正確或尚未建立維修單。"
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

    /// <summary>
    /// 將資料庫工單物件轉換成維修紀錄 DTO。
    /// </summary>
    /// <param name="order">資料庫工單資料。</param>
    /// <returns>維修紀錄 DTO。</returns>
    private static CarPlateMaintenanceRecordDto MapToMaintenanceRecord(Order order)
    {
        return new CarPlateMaintenanceRecordDto
        {
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo,
            OrderDate = order.Date,
            CreatedAt = order.CreationTimestamp,
            Status = order.Status,
            FixType = order.FixType,
            Amount = order.Amount,
            WorkDate = order.WorkDate,
            Remark = order.Remark
        };
    }

    /// <summary>
    /// 解析工單可用的排序日期，優先使用工單日期，其次為排程開工日與建立時間。
    /// </summary>
    /// <param name="order">資料庫工單資料。</param>
    /// <returns>排序使用的日期時間。</returns>
    private static DateTime? ResolveRecordDate(Order order)
    {
        if (order.Date.HasValue)
        {
            return order.Date.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (!string.IsNullOrWhiteSpace(order.WorkDate) && DateTime.TryParse(order.WorkDate, out var workDate))
        {
            return workDate;
        }

        if (order.Status210Date.HasValue)
        {
            return order.Status210Date;
        }

        if (order.Status220Date.HasValue)
        {
            return order.Status220Date;
        }

        if (order.Status290Date.HasValue)
        {
            return order.Status290Date;
        }

        if (order.Status295Timestamp.HasValue)
        {
            return order.Status295Timestamp;
        }

        return order.CreationTimestamp;
    }
}
