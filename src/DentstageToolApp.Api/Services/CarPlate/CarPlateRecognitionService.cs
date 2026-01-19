using DentstageToolApp.Api.Models.CarPlates;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DentstageToolApp.Api.Services.CarPlate;

/// <summary>
/// 車牌辨識服務，支援 EasyOCR 引擎，負責整合影像辨識與資料庫查詢。
/// </summary>
public class CarPlateRecognitionService : ICarPlateRecognitionService
{
    private static readonly Regex PlateCandidateRegex = new("[A-Z0-9]{4,10}", RegexOptions.Compiled);

    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CarPlateRecognitionService> _logger;
    private readonly string _ocrMode;
    private readonly EasyOcrOptions _easyOcrOptions;


    /// <summary>
    /// 建構子，注入 OCR 組態與資料庫內容類別。
    /// </summary>
    public CarPlateRecognitionService(
        IConfiguration configuration,
        IOptions<EasyOcrOptions> easyOcrOptions,
        DentstageToolAppContext dbContext,
        ILogger<CarPlateRecognitionService> logger)
    {
        _ocrMode = configuration["OcrMode"]?.ToLowerInvariant() ?? "easyocr";
        _easyOcrOptions = easyOcrOptions?.Value ?? new EasyOcrOptions();
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
            throw new InvalidDataException("影像內容為空，請重新上傳清晰的車牌照照。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- OCR 解析區 ----------
        var (rawText, confidence) = await RunOcrAsync(imageSource.ImageBytes, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            _logger.LogWarning("OCR 未從影像讀取到任何文字，檔案名稱：{FileName}", imageSource.FileName);
            return null;
        }

        var candidatePlate = ExtractPlateCandidate(rawText);
        if (string.IsNullOrWhiteSpace(candidatePlate))
        {
            _logger.LogWarning("OCR 雖取得文字但未找到符合格式的車牌，檔案名稱：{FileName}", imageSource.FileName);
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
                .ThenInclude(order => order.Quatation)
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                // 精準比對（原有行為）
                c.CarNo == normalizedPlate
                || c.CarNo == candidatePlate
                || c.CarNoQuery == normalizedPlate
                // 新增模糊比對：支援部分車牌或省略連字號的查詢
             || (c.CarNoQuery != null && c.CarNoQuery.Contains(normalizedPlate))
             || (c.CarNo != null && c.CarNo.Contains(normalizedPlate))
             || (c.CarNoQuery != null && c.CarNoQuery.Contains(normalizedPlate)),
            cancellationToken);

        // ---------- 組裝回應區 ----------
        var hasMaintenanceRecords = car?.Orders?.Any() == true;

        // 從工單與報價單中解析 BrandUid 和 ModelUid（與 GetMaintenanceHistoryAsync 一致）
        var orders = car?.Orders?.ToList() ?? new List<Order>();
        var referenceOrder = orders.FirstOrDefault();

        var quotationCandidates = orders
            .Select(order => order.Quatation)
            .Where(quatation => quatation is not null)
            .Cast<Quatation>()
            .ToList();

        var referenceQuotation = quotationCandidates.FirstOrDefault();

        // 若無對應報價單，額外查詢同車牌的最新估價資料
        if (referenceQuotation is null)
        {
            referenceQuotation = await _dbContext.Quatations
                .AsNoTracking()
                .Where(quatation =>
                    quatation.CarNo == normalizedPlate
                    || quatation.CarNo == candidatePlate
                    || quatation.CarNoInput == normalizedPlate
                    || quatation.CarNoInput == candidatePlate
                    || quatation.CarNoInputGlobal == candidatePlate
                    || (quatation.CarNo != null && quatation.CarNo.Contains(normalizedPlate))
                    || (quatation.CarNoInput != null && quatation.CarNoInput.Contains(candidatePlate))
                    || (quatation.CarNoInputGlobal != null && quatation.CarNoInputGlobal.Contains(candidatePlate)))
                .OrderByDescending(quatation => quatation.ModificationTimestamp ?? quatation.CreationTimestamp ?? DateTime.MinValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (referenceQuotation is not null)
            {
                quotationCandidates.Add(referenceQuotation);
            }
        }

        // 品牌與車型識別碼僅存於估價資料，收集所有報價資訊後挑選第一筆有效值
        var brandUid = ResolveFirstValue(
            BuildCandidateList(
                referenceOrder?.Quatation?.BrandUid,
                referenceQuotation?.BrandUid,
                orders.Select(o => o.Quatation?.BrandUid),
                quotationCandidates.Select(quatation => quatation.BrandUid)));

        var modelUid = ResolveFirstValue(
            BuildCandidateList(
                referenceOrder?.Quatation?.ModelUid,
                referenceQuotation?.ModelUid,
                orders.Select(o => o.Quatation?.ModelUid),
                quotationCandidates.Select(quatation => quatation.ModelUid)));
        
        var response = new CarPlateRecognitionResponse
        {
            RecognitionCarPlateNumber = normalizedPlate,
            CarPlateNumber = car?.CarNo ?? normalizedPlate,
            Confidence = Math.Round(confidence, 2),
            CarUid = car?.CarUid,
            BrandUid = brandUid,
            ModelUid = modelUid,
            Brand = car?.Brand,
            Model = car?.Model,
            Color = car?.Color,
            HasMaintenanceRecords = hasMaintenanceRecords,
            HasMaintenanceHistory = hasMaintenanceRecords,
            Milage = car?.Milage,
            CarRemark = car?.CarRemark,
            Message = hasMaintenanceRecords
                ? "查詢成功，已列出歷史維修資料。"
                : car is null
                    ? "查無維修紀錄，請確認車牌是否正確或尚未建立維修單。"
                    : "查無維修紀錄，請確認車牌是否正確或尚未建立維修單。",
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
                .ThenInclude(order => order.Quatation)
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                // 精準比對
                c.CarNo == normalizedPlate
                || c.CarNo == trimmedPlate
                || c.CarNoQuery == normalizedPlate
                || c.CarNoQuery == trimmedPlate
                // 模糊比對（與 RecognizeAsync 保持一致）
                || (c.CarNoQuery != null && c.CarNoQuery.Contains(normalizedPlate))
                || (c.CarNo != null && c.CarNo.Contains(normalizedPlate))
                || (c.CarNoQuery != null && c.CarNoQuery.Contains(normalizedPlate)),
            cancellationToken);

        var orders = new List<Order>();
                    

        if (car is not null)
        {
            // 如果透過模糊或精準查到車輛，直接以該車輛的第一筆資料為主，並僅回傳該車的工單紀錄。
            // 這可避免在模糊查詢時把不同車牌（或類似字串）的工單混雜在一起。
            if (car.Orders is { Count: > 0 })
            {
                orders.AddRange(car.Orders);
            }
        }
        else
        {
            // 若找不到車輛主檔，保留原有的行為：在 Orders 表中以多欄位進行精準或模糊比對，收集相關工單。
            var additionalOrders = await _dbContext.Orders
                .Include(order => order.Quatation)
                .AsNoTracking()
                .Where(o =>
                    // 精準比對
                    o.CarNo == normalizedPlate
                    || o.CarNo == trimmedPlate
                    || o.CarNoInput == normalizedPlate
                    || o.CarNoInput == trimmedPlate
                    || o.CarNoInputGlobal == trimmedPlate
                    // 模糊比對：包含 normalized 或 原始輸入的 trimmedPlate
                    || (o.CarNo != null && o.CarNo.Contains(normalizedPlate))
                    || (o.CarNoInput != null && o.CarNoInput.Contains(trimmedPlate))
                    || (o.CarNoInputGlobal != null && o.CarNoInputGlobal.Contains(trimmedPlate)))
                .ToListAsync(cancellationToken);

            orders.AddRange(additionalOrders);
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

        // ---------- 報價資料補強區 ----------
        // 先收集所有工單已載入的報價單，方便後續統一挑選車輛資訊來源。
        var quotationCandidates = orders
            .Select(order => order.Quatation)
            .Where(quatation => quatation is not null)
            .Cast<Quatation>()
            .ToList();

        var referenceQuotation = quotationCandidates.FirstOrDefault();

        if (referenceQuotation is null)
        {
            // 若工單無對應報價單，額外查詢同車牌的最新估價資料，補齊車輛欄位。
            referenceQuotation = await _dbContext.Quatations
                .AsNoTracking()
                .Where(quatation =>
                    // 精準比對
                    quatation.CarNo == normalizedPlate
                    || quatation.CarNo == trimmedPlate
                    || quatation.CarNoInput == normalizedPlate
                    || quatation.CarNoInput == trimmedPlate
                    || quatation.CarNoInputGlobal == trimmedPlate
                    // 模糊比對
                    || (quatation.CarNo != null && quatation.CarNo.Contains(normalizedPlate))
                    || (quatation.CarNoInput != null && quatation.CarNoInput.Contains(trimmedPlate))
                    || (quatation.CarNoInputGlobal != null && quatation.CarNoInputGlobal.Contains(trimmedPlate)))
                .OrderByDescending(quatation => quatation.ModificationTimestamp ?? quatation.CreationTimestamp ?? DateTime.MinValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (referenceQuotation is not null)
            {
                quotationCandidates.Add(referenceQuotation);
            }
        }

        // ---------- 車輛識別整理區 ----------
        // 依序從車輛主檔、工單與估價單補齊識別碼，避免資料缺漏。
        var carUid = ResolveFirstValue(
            BuildCandidateList(
                car?.CarUid,
                referenceOrder?.CarUid,
                referenceOrder?.Quatation?.CarUid,
                referenceQuotation?.CarUid,
                orders.Select(o => o.CarUid),
                orders.Select(o => o.Quatation?.CarUid),
                quotationCandidates.Select(quatation => quatation.CarUid)));

        // 品牌與車型識別碼僅存於估價資料，因此收集所有工單估價資訊後挑選第一筆有效值。
        var brandUid = ResolveFirstValue(
            BuildCandidateList(
                referenceOrder?.Quatation?.BrandUid,
                referenceQuotation?.BrandUid,
                orders.Select(o => o.Quatation?.BrandUid),
                quotationCandidates.Select(quatation => quatation.BrandUid)));

        var modelUid = ResolveFirstValue(
            BuildCandidateList(
                referenceOrder?.Quatation?.ModelUid,
                referenceQuotation?.ModelUid,
                orders.Select(o => o.Quatation?.ModelUid),
                quotationCandidates.Select(quatation => quatation.ModelUid)));

        var brand = ResolveFirstValue(
            BuildCandidateList(
                car?.Brand,
                referenceOrder?.Brand,
                referenceOrder?.Quatation?.Brand,
                referenceQuotation?.Brand,
                orders.Select(order => order.Brand),
                orders.Select(order => order.Quatation?.Brand),
                quotationCandidates.Select(quatation => quatation.Brand)));

        var model = ResolveFirstValue(
            BuildCandidateList(
                car?.Model,
                referenceOrder?.Model,
                referenceOrder?.Quatation?.Model,
                referenceQuotation?.Model,
                orders.Select(order => order.Model),
                orders.Select(order => order.Quatation?.Model),
                quotationCandidates.Select(quatation => quatation.Model)));

        var color = ResolveFirstValue(
            BuildCandidateList(
                car?.Color,
                referenceOrder?.Color,
                referenceOrder?.Quatation?.Color,
                referenceQuotation?.Color,
                orders.Select(order => order.Color),
                orders.Select(order => order.Quatation?.Color),
                quotationCandidates.Select(quatation => quatation.Color)));

        var carRemark = ResolveFirstValue(
            BuildCandidateList(
                car?.CarRemark,
                referenceOrder?.CarRemark,
                referenceOrder?.Quatation?.CarRemark,
                referenceQuotation?.CarRemark,
                orders.Select(order => order.CarRemark),
                orders.Select(order => order.Quatation?.CarRemark),
                quotationCandidates.Select(quatation => quatation.CarRemark)));

        var milageCandidates = new List<int?>
        {
            car?.Milage,
            referenceOrder?.Milage,
            referenceOrder?.Quatation?.Milage,
            referenceQuotation?.Milage
        };

        milageCandidates.AddRange(orders.Select(order => order.Milage));
        milageCandidates.AddRange(orders.Select(order => order.Quatation?.Milage));
        milageCandidates.AddRange(quotationCandidates.Select(quatation => quatation.Milage));

        var milage = ResolveFirstValue(milageCandidates);

        var response = new CarPlateMaintenanceHistoryResponse
        {
            RecognitionCarPlateNumber = normalizedPlate,
            CarPlateNumber = car?.CarNo ?? normalizedPlate,
            CarUid = carUid,
            BrandUid = brandUid,
            ModelUid = modelUid,
            Brand = brand,
            Model = model,
            Color = color,
            HasMaintenanceRecords = recordItems.Count > 0,
            Milage = milage,
            CarRemark = carRemark,
            Records = recordItems,
            Message = recordItems.Count > 0
                ? "查詢成功，已列出歷史維修資料。"
                : "查無維修紀錄，請確認車牌是否正確或尚未建立維修單。"
        };

        return response;
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 建立候選清單，將主要值與額外來源合併後輸出為列舉。
    /// </summary>
    /// <param name="primary">優先檢查的主要值。</param>
    /// <param name="secondary">次要值集合，可為 null，可傳入單一字串或可列舉集合。</param>
    /// <returns>含所有候選字串的列舉。</returns>
    private static IEnumerable<string?> BuildCandidateList(string? primary, params object?[] secondary)
    {
        // 先回傳主要值，確保主檔資訊優先使用。
        yield return primary;

        // 依序回傳其他來源的值，每個來源皆可能為 null。
        foreach (var sequence in secondary)
        {
            if (sequence is null)
            {
                continue;
            }

            // 若來源是單一字串則直接回傳，確保單筆資料可被納入比對。
            if (sequence is string singleCandidate)
            {
                yield return singleCandidate;
                continue;
            }

            // 若來源為集合則逐一回傳，支援多筆資料合併。
            if (sequence is IEnumerable<string?> enumerable)
            {
                foreach (var item in enumerable)
                {
                    yield return item;
                }

                continue;
            }

            // 其餘型別視為不支援，避免出現未預期的型別造成例外或錯誤。
            throw new InvalidOperationException("BuildCandidateList 僅支援 string 或 IEnumerable<string?> 型別作為輸入來源。");
        }
    }

    /// <summary>
    /// 從候選集合中挑選第一個有效值，並移除前後空白。
    /// </summary>
    /// <param name="candidates">候選字串集合。</param>
    /// <returns>第一個非空白的字串；若無則回傳 null。</returns>
    private static string? ResolveFirstValue(IEnumerable<string?> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            return candidate.Trim();
        }

        return null;
    }

    /// <summary>
    /// 從候選集合中挑選第一個有值的數值型別，適用於里程等欄位。
    /// </summary>
    /// <typeparam name="T">值型別，例如 int 或 decimal。</typeparam>
    /// <param name="candidates">候選集合。</param>
    /// <returns>第一個有值的元素；若皆為 null 則回傳 null。</returns>
    private static T? ResolveFirstValue<T>(IEnumerable<T?> candidates)
        where T : struct
    {
        foreach (var candidate in candidates)
        {
            if (candidate.HasValue)
            {
                return candidate.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 執行車牌辨識，使用 EasyOCR 引擎。
    /// </summary>
    /// <param name="imageBytes">影像的位元組陣列。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>影像中的文字與信心度百分比。</returns>
    private async Task<(string Text, double Confidence)> RunOcrAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("使用車牌辨識模式：{OcrMode}", _ocrMode);

        // 使用 EasyOCR 引擎
        var preprocessed = PreprocessForOcr(imageBytes, targetWidth: 1200, blurSigma: 1.2f, doAutoContrast: true);
        return await RunEasyOcrAsync(preprocessed, cancellationToken);
    }

    /// <summary>
    /// 呼叫 EasyOCR 進行車牌辨識（支援 Local 和 API 模式）。
    /// </summary>
    private async Task<(string Text, double Confidence)> RunEasyOcrAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_easyOcrOptions.UseLocal)
        {
            return await RunEasyOcrLocalAsync(imageBytes, cancellationToken);
        }
        else
        {
            return await RunEasyOcrApiAsync(imageBytes, cancellationToken);
        }
    }

    /// <summary>
    /// 使用本機 Python 腳本執行 EasyOCR 辨識。
    /// </summary>
    private async Task<(string Text, double Confidence)> RunEasyOcrLocalAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
        try
        {
            await File.WriteAllBytesAsync(tempFile, imageBytes, cancellationToken);

            var languagesArg = string.Join(",", _easyOcrOptions.Languages);
            var gpuFlag = _easyOcrOptions.GpuEnabled ? "--gpu" : "--no-gpu";
            var args = $"\"{_easyOcrOptions.ScriptPath}\" \"{tempFile}\" --languages {languagesArg} {gpuFlag}";

            var psi = new ProcessStartInfo(_easyOcrOptions.PythonPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                throw new InvalidOperationException($"無法啟動 EasyOCR Python 腳本，Python 路徑：{_easyOcrOptions.PythonPath}");
            }

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException($"EasyOCR 執行失敗 (exit code: {proc.ExitCode})\nstderr: {error}");
            }

            // 解析 JSON 輸出：{"text": "ABC1234", "confidence": 0.95}
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            var text = root.GetProperty("text").GetString() ?? string.Empty;
            var confidence = root.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number
                ? confEl.GetDouble() * 100d  // 轉換為百分比
                : 0d;

            return (text, confidence);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// 呼叫遠端 EasyOCR API 執行辨識。
    /// </summary>
    private async Task<(string Text, double Confidence)> RunEasyOcrApiAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_easyOcrOptions.ApiUrl))
        {
            throw new InvalidOperationException("EasyOCR API 模式已啟用但未設定 ApiUrl，請於 appsettings.json 設定 EasyOcr.ApiUrl。");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "plate.png");

        // 加入語言參數
        if (_easyOcrOptions.Languages.Any())
        {
            content.Add(new StringContent(string.Join(",", _easyOcrOptions.Languages)), "languages");
        }

        // 加入 GPU 參數
        content.Add(new StringContent(_easyOcrOptions.GpuEnabled.ToString().ToLower()), "gpu_enabled");

        var response = await httpClient.PostAsync(_easyOcrOptions.ApiUrl, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"EasyOCR API 呼叫失敗 (HTTP {(int)response.StatusCode})：{errorContent}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // 解析 API 回應：{"text": "ABC1234", "confidence": 0.95}
        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        var text = root.GetProperty("text").GetString() ?? string.Empty;
        var confidence = root.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number
            ? confEl.GetDouble() * 100d  // 轉換為百分比
            : 0d;

        return (text, confidence);
    }

    /// <summary>
    /// 前處理影像以供 OCR 使用：縮放、灰階、可選自動對比以及高斯模糊，最後輸出 PNG bytes。
    /// </summary>
    /// <param name="inputImage">輸入影像位元組。</param>
    /// <param name="targetWidth">目標寬度（保留比例），若 <=0 則不縮放。</param>
    /// <param name="blurSigma">Gaussian blur sigma 值，若 <=0 則不做模糊。</param>
    /// <param name="doAutoContrast">是否套用自動對比增強。</param>
    /// <returns>PNG 格式的位元組陣列，可直接傳入 Pix.LoadFromMemory。</returns>
    private byte[] PreprocessForOcr(byte[] inputImage, int targetWidth = 1024, float blurSigma = 1.5f, bool doAutoContrast = true)
    {
        using var image = Image.Load<Rgba32>(inputImage);

        // 縮放保留比例
        if (targetWidth > 0 && image.Width > 0 && image.Width != targetWidth)
        {
            var newHeight = (int)Math.Round(image.Height * (targetWidth / (double)image.Width));
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, newHeight),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        // 轉灰階
        image.Mutate(x => x.Grayscale());

        // 自動對比（可選）— 使用 Contrast 調整（AutoContrast 在某些版本的 ImageSharp 可能不可用）
        if (doAutoContrast)
        {
            // 輕微提高對比度，數值可調（1.0 = 無變化）
            image.Mutate(x => x.Contrast(1.1f));
        }

        // Gaussian blur 去噪
        if (blurSigma > 0.01f)
        {
            image.Mutate(x => x.GaussianBlur(blurSigma));
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
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
        // 將所有非英數字的符號（例如 - _ . / 空白等）統一轉為空白，避免把字連在一起
        var cleaned = Regex.Replace(uppercaseText, "[^A-Z0-9]+", "");

        // 先依 token 檢查（避免跨 token 拼接造成誤判）
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var normalizedToken = NormalizePlate(token);
            if (!string.IsNullOrWhiteSpace(normalizedToken) && normalizedToken.Length is >= 5 and <= 8)
            {
                return normalizedToken;
            }
        }

        // 退回以連續英數字的正則比對（在已清理的字串上進行）
        var matches = PlateCandidateRegex.Matches(cleaned);

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
            return order.Date.Value.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.Zero));
        }

        if (!string.IsNullOrWhiteSpace(order.WorkDate) && DateTime.TryParse(order.WorkDate, out var workDate))
        {
            return workDate;
        }

        if (order.Status220Date.HasValue)
        {
            return order.Status220Date;
        }

        if (order.Status290Date.HasValue)
        {
            return order.Status290Date;
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
