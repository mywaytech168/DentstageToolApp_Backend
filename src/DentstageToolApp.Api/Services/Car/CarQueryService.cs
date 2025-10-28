using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Cars;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Models.Pagination;
using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Services.Shared;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CarEntity = DentstageToolApp.Infrastructure.Entities.Car;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛查詢服務實作，負責提供車輛列表與明細資料。
/// </summary>
public class CarQueryService : ICarQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CarQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public CarQueryService(DentstageToolAppContext dbContext, ILogger<CarQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CarListResponse> GetCarsAsync(PaginationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pagination = request ?? new PaginationRequest();
            var (page, pageSize) = pagination.Normalize();

            _logger.LogDebug(
                "開始查詢車輛列表資料，頁碼：{Page}，每頁筆數：{PageSize}。",
                page,
                pageSize);

            var items = await _dbContext.Cars
                .AsNoTracking()
                .OrderByDescending(car => car.CreationTimestamp)
                .ThenBy(car => car.CarUid)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(car => new CarListItem
                {
                    CarUid = car.CarUid,
                    CarPlateNumber = car.CarNo,
                    Brand = car.Brand,
                    Model = car.Model,
                    Mileage = car.Milage,
                    CreatedAt = car.CreationTimestamp
                })
                .ToListAsync(cancellationToken);

            var totalCount = await _dbContext.Cars.CountAsync(cancellationToken);

            _logger.LogInformation(
                "車輛列表查詢完成，頁碼：{Page}，共取得 {Count} / {Total} 筆資料。",
                page,
                items.Count,
                totalCount);

            return new CarListResponse
            {
                Items = items,
                Pagination = new PaginationMetadata
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("車輛列表查詢流程被取消。");
            throw new CarQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (CarQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢車輛列表時發生未預期錯誤。");
            throw new CarQueryServiceException(HttpStatusCode.InternalServerError, "查詢車輛列表發生錯誤，請稍後再試。");
        }
    }

    /// <inheritdoc />
    public async Task<CarDetailResponse> GetCarAsync(string carUid, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUid = (carUid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                throw new CarQueryServiceException(HttpStatusCode.BadRequest, "請提供車輛識別碼。");
            }

            _logger.LogDebug("查詢車輛明細，UID：{CarUid}。", normalizedUid);

            var entity = await _dbContext.Cars
                .AsNoTracking()
                .FirstOrDefaultAsync(car => car.CarUid == normalizedUid, cancellationToken);

            if (entity is null)
            {
                throw new CarQueryServiceException(HttpStatusCode.NotFound, "找不到對應的車輛資料。");
            }

            return new CarDetailResponse
            {
                CarUid = entity.CarUid,
                CarPlateNumber = entity.CarNo,
                Brand = entity.Brand,
                Model = entity.Model,
                Color = entity.Color,
                Remark = entity.CarRemark,
                Mileage = entity.Milage,
                CreatedAt = entity.CreationTimestamp,
                UpdatedAt = entity.ModificationTimestamp,
                CreatedBy = entity.CreatedBy,
                ModifiedBy = entity.ModifiedBy
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("車輛明細查詢流程被取消。");
            throw new CarQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (CarQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢車輛明細時發生未預期錯誤。");
            throw new CarQueryServiceException(HttpStatusCode.InternalServerError, "查詢車輛明細發生錯誤，請稍後再試。");
        }
    }

    /// <inheritdoc />
    public async Task<CarPlateSearchResponse> SearchByPlateAsync(CarPlateSearchRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CarQueryServiceException(HttpStatusCode.BadRequest, "請提供查詢條件。");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPlate = NormalizePlate(request.CarPlate);
            if (string.IsNullOrWhiteSpace(normalizedPlate))
            {
                throw new CarQueryServiceException(HttpStatusCode.BadRequest, "請輸入欲查詢的車牌號碼。");
            }

            var plateKey = ExtractPlateKey(normalizedPlate);

            _logger.LogInformation(
                "執行車牌搜尋，關鍵字：{Plate}，比對字串：{Key}。",
                normalizedPlate,
                plateKey);

            var carsQuery = _dbContext.Cars.AsNoTracking();
            if (!string.IsNullOrEmpty(plateKey))
            {
                var digitsPattern = $"%{plateKey}%";
                carsQuery = carsQuery.Where(car =>
                    (car.CarNoQuery != null && EF.Functions.Like(car.CarNoQuery, digitsPattern))
                    || (car.CarNo != null && EF.Functions.Like(car.CarNo, $"%{normalizedPlate}%")));
            }
            else
            {
                var rawPattern = $"%{normalizedPlate}%";
                carsQuery = carsQuery.Where(car =>
                    car.CarNo != null && EF.Functions.Like(car.CarNo, rawPattern));
            }

            var carEntities = await carsQuery.ToListAsync(cancellationToken);

            var relatedQuotations = await FetchQuotationsByCarAsync(
                normalizedPlate,
                plateKey,
                carEntities,
                cancellationToken);

            var relatedOrders = await FetchOrdersByCarAsync(
                normalizedPlate,
                plateKey,
                carEntities,
                cancellationToken);

            var quotationSummaries = await SummaryMappingHelper.BuildQuotationSummariesAsync(
                _dbContext,
                relatedQuotations,
                cancellationToken);

            var orderSummaries = await SummaryMappingHelper.BuildMaintenanceSummariesAsync(
                _dbContext,
                relatedOrders,
                cancellationToken);

            var quotationMap = BuildCarQuotationMap(carEntities, relatedQuotations, quotationSummaries);
            var orderMap = BuildCarOrderMap(carEntities, relatedOrders, orderSummaries);

            var carItems = carEntities
                .Select(car => MapToCarPlateItem(car, quotationMap, orderMap))
                .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
                .ToList();

            // 搜尋結果只需呈現單筆車輛，因此取排序後的第一筆車輛資料。
            var selectedCar = carItems.FirstOrDefault();

            var response = new CarPlateSearchResponse
            {
                QueryPlate = normalizedPlate,
                QueryPlateKey = plateKey,
                Car = selectedCar,
                Message = BuildCarSearchMessage(carItems.Count, quotationSummaries.Count, orderSummaries.Count)
            };

            _logger.LogInformation(
                "車牌搜尋完成，找到 {CarCount} 輛車、{QuotationCount} 筆估價單、{OrderCount} 筆維修單。",
                carItems.Count,
                quotationSummaries.Count,
                orderSummaries.Count);

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("車牌搜尋流程被取消。");
            throw new CarQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (CarQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "車牌搜尋時發生未預期錯誤。");
            throw new CarQueryServiceException(HttpStatusCode.InternalServerError, "車牌搜尋發生錯誤，請稍後再試。");
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 以車牌關鍵字與車輛清單取得相關估價單資料。
    /// </summary>
    private async Task<List<Quatation>> FetchQuotationsByCarAsync(
        string normalizedPlate,
        string plateKey,
        IReadOnlyCollection<CarEntity> carEntities,
        CancellationToken cancellationToken)
    {
        var quotations = new Dictionary<string, Quatation>(StringComparer.OrdinalIgnoreCase);

        var rawPattern = $"%{normalizedPlate}%";
        var quotationsByRaw = await _dbContext.Quatations
            .AsNoTracking()
            .Where(quotation =>
                (quotation.CarNo != null && EF.Functions.Like(quotation.CarNo, rawPattern))
                || (quotation.CarNoInput != null && EF.Functions.Like(quotation.CarNoInput, rawPattern))
                || (quotation.CarNoInputGlobal != null && EF.Functions.Like(quotation.CarNoInputGlobal, rawPattern)))
            .ToListAsync(cancellationToken);

        MergeQuotations(quotations, quotationsByRaw);

        if (!string.IsNullOrEmpty(plateKey))
        {
            var digitsPattern = $"%{plateKey}%";
            var quotationsByDigits = await _dbContext.Quatations
                .AsNoTracking()
                .Where(quotation =>
                    (quotation.CarNo != null && EF.Functions.Like(quotation.CarNo, digitsPattern))
                    || (quotation.CarNoInput != null && EF.Functions.Like(quotation.CarNoInput, digitsPattern))
                    || (quotation.CarNoInputGlobal != null && EF.Functions.Like(quotation.CarNoInputGlobal, digitsPattern)))
                .ToListAsync(cancellationToken);

            MergeQuotations(quotations, quotationsByDigits);
        }

        if (carEntities.Count > 0)
        {
            var carUids = carEntities
                .Select(car => NormalizeOptionalText(car.CarUid))
                .Where(uid => uid is not null)
                .Select(uid => uid!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (carUids.Count > 0)
            {
                var quotationsByCar = await _dbContext.Quatations
                    .AsNoTracking()
                    .Where(quotation =>
                        quotation.CarUid != null && carUids.Contains(quotation.CarUid))
                    .ToListAsync(cancellationToken);

                MergeQuotations(quotations, quotationsByCar);
            }
        }

        return quotations
            .Values
            .OrderByDescending(quotation => quotation.CreationTimestamp ?? DateTime.MinValue)
            .ThenByDescending(quotation => quotation.QuotationNo)
            .ToList();
    }

    /// <summary>
    /// 以車牌關鍵字取得相關維修單資料。
    /// </summary>
    private async Task<List<Order>> FetchOrdersByCarAsync(
        string normalizedPlate,
        string plateKey,
        IReadOnlyCollection<CarEntity> carEntities,
        CancellationToken cancellationToken)
    {
        var orders = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);

        var rawPattern = $"%{normalizedPlate}%";
        var ordersByRaw = await _dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                (order.CarNo != null && EF.Functions.Like(order.CarNo, rawPattern))
                || (order.CarNoInput != null && EF.Functions.Like(order.CarNoInput, rawPattern))
                || (order.CarNoInputGlobal != null && EF.Functions.Like(order.CarNoInputGlobal, rawPattern)))
            .ToListAsync(cancellationToken);

        MergeOrders(orders, ordersByRaw);

        if (!string.IsNullOrEmpty(plateKey))
        {
            var digitsPattern = $"%{plateKey}%";
            var ordersByDigits = await _dbContext.Orders
                .AsNoTracking()
                .Where(order =>
                    (order.CarNo != null && EF.Functions.Like(order.CarNo, digitsPattern))
                    || (order.CarNoInput != null && EF.Functions.Like(order.CarNoInput, digitsPattern))
                    || (order.CarNoInputGlobal != null && EF.Functions.Like(order.CarNoInputGlobal, digitsPattern)))
                .ToListAsync(cancellationToken);

            MergeOrders(orders, ordersByDigits);
        }

        if (carEntities.Count > 0)
        {
            var carUids = carEntities
                .Select(car => NormalizeOptionalText(car.CarUid))
                .Where(uid => uid is not null)
                .Select(uid => uid!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (carUids.Count > 0)
            {
                var ordersByCar = await _dbContext.Orders
                    .AsNoTracking()
                    .Where(order =>
                        order.CarUid != null && carUids.Contains(order.CarUid))
                    .ToListAsync(cancellationToken);

                MergeOrders(orders, ordersByCar);
            }
        }

        return orders
            .Values
            .OrderByDescending(order => order.CreationTimestamp ?? DateTime.MinValue)
            .ThenByDescending(order => order.OrderNo)
            .ToList();
    }

    /// <summary>
    /// 建立車輛與估價單摘要的對照表。
    /// </summary>
    private static IReadOnlyDictionary<string, List<QuotationSummaryResponse>> BuildCarQuotationMap(
        IReadOnlyCollection<CarEntity> carEntities,
        IReadOnlyList<Quatation> quotations,
        IReadOnlyList<QuotationSummaryResponse> summaries)
    {
        var (carUidSet, plateMap) = BuildCarLookup(carEntities);
        var map = new Dictionary<string, List<QuotationSummaryResponse>>(StringComparer.OrdinalIgnoreCase);
        var count = Math.Min(quotations.Count, summaries.Count);

        for (var i = 0; i < count; i++)
        {
            var quotation = quotations[i];
            var summary = summaries[i];
            var normalizedCarUid = NormalizeOptionalText(quotation.CarUid);
            var assigned = false;

            if (normalizedCarUid is not null && carUidSet.Contains(normalizedCarUid))
            {
                AppendSummary(map, normalizedCarUid, summary);
                assigned = true;
            }

            if (assigned)
            {
                continue;
            }

            var normalizedPlate = NormalizePlateKey(
                quotation.CarNo
                ?? quotation.CarNoInput
                ?? quotation.CarNoInputGlobal);

            if (normalizedPlate is null)
            {
                continue;
            }

            if (!plateMap.TryGetValue(normalizedPlate, out var carUidList))
            {
                continue;
            }

            foreach (var carUid in carUidList)
            {
                AppendSummary(map, carUid, summary);
            }
        }

        SortSummaries(map, item => item.CreatedAt, item => item.QuotationNo);

        return map;
    }

    /// <summary>
    /// 建立車輛與維修單摘要的對照表。
    /// </summary>
    private static IReadOnlyDictionary<string, List<MaintenanceOrderSummaryResponse>> BuildCarOrderMap(
        IReadOnlyCollection<CarEntity> carEntities,
        IReadOnlyList<Order> orders,
        IReadOnlyList<MaintenanceOrderSummaryResponse> summaries)
    {
        var (carUidSet, plateMap) = BuildCarLookup(carEntities);
        var map = new Dictionary<string, List<MaintenanceOrderSummaryResponse>>(StringComparer.OrdinalIgnoreCase);
        var count = Math.Min(orders.Count, summaries.Count);

        for (var i = 0; i < count; i++)
        {
            var order = orders[i];
            var summary = summaries[i];
            var normalizedCarUid = NormalizeOptionalText(order.CarUid);
            var assigned = false;

            if (normalizedCarUid is not null && carUidSet.Contains(normalizedCarUid))
            {
                AppendSummary(map, normalizedCarUid, summary);
                assigned = true;
            }

            if (assigned)
            {
                continue;
            }

            var normalizedPlate = NormalizePlateKey(
                order.CarNo
                ?? order.CarNoInput
                ?? order.CarNoInputGlobal);

            if (normalizedPlate is null)
            {
                continue;
            }

            if (!plateMap.TryGetValue(normalizedPlate, out var carUidList))
            {
                continue;
            }

            foreach (var carUid in carUidList)
            {
                AppendSummary(map, carUid, summary);
            }
        }

        SortSummaries(map, item => item.CreatedAt, item => item.OrderNo);

        return map;
    }

    /// <summary>
    /// 建立車輛查詢回傳項目。
    /// </summary>
    private static CarPlateSearchItem MapToCarPlateItem(
        CarEntity car,
        IReadOnlyDictionary<string, List<QuotationSummaryResponse>> quotationMap,
        IReadOnlyDictionary<string, List<MaintenanceOrderSummaryResponse>> orderMap)
    {
        var normalizedUid = NormalizeOptionalText(car.CarUid) ?? string.Empty;

        var quotations = quotationMap.TryGetValue(normalizedUid, out var quotationList)
            ? (IReadOnlyCollection<QuotationSummaryResponse>)quotationList
            : Array.Empty<QuotationSummaryResponse>();

        var orders = orderMap.TryGetValue(normalizedUid, out var orderList)
            ? (IReadOnlyCollection<MaintenanceOrderSummaryResponse>)orderList
            : Array.Empty<MaintenanceOrderSummaryResponse>();

        // 先拆解品牌與型號，避免資料庫僅填寫合併欄位時出現缺漏。
        var (brand, model) = ResolveCarBrandAndModel(car);

        return new CarPlateSearchItem
        {
            // 使用正規化後的 UID，確保前後端比對時不受多餘空白影響。
            CarUid = normalizedUid,
            // 將車牌資料與其他描述欄位一併正規化，提供乾淨的字串給前端顯示。
            CarPlateNumber = NormalizeOptionalText(car.CarNo),
            Brand = brand,
            Model = model,
            Color = NormalizeOptionalText(car.Color),
            Remark = NormalizeOptionalText(car.CarRemark),
            Mileage = car.Milage,
            CreatedAt = car.CreationTimestamp,
            UpdatedAt = car.ModificationTimestamp,
            Quotations = quotations,
            MaintenanceOrders = orders
        };
    }

    /// <summary>
    /// 建立車輛查詢的提示訊息。
    /// </summary>
    private static string BuildCarSearchMessage(int carCount, int quotationCount, int orderCount)
    {
        if (carCount == 0 && quotationCount == 0 && orderCount == 0)
        {
            return "查無符合的車輛與單據資料。";
        }

        if (carCount == 0)
        {
            return "查無車輛資料，但已回傳相關單據供參考。";
        }

        if (quotationCount == 0 && orderCount == 0)
        {
            return "已找到車輛資料，目前尚無估價或維修紀錄。";
        }

        return "查詢成功，已回傳車輛與相關單據。";
    }

    /// <summary>
    /// 合併估價單集合，避免重複加入相同報價單。
    /// </summary>
    private static void MergeQuotations(IDictionary<string, Quatation> target, IEnumerable<Quatation> source)
    {
        foreach (var quotation in source)
        {
            if (string.IsNullOrWhiteSpace(quotation.QuotationUid))
            {
                continue;
            }

            if (target.ContainsKey(quotation.QuotationUid))
            {
                continue;
            }

            target[quotation.QuotationUid] = quotation;
        }
    }

    /// <summary>
    /// 合併維修單集合，避免重複加入相同工單。
    /// </summary>
    private static void MergeOrders(IDictionary<string, Order> target, IEnumerable<Order> source)
    {
        foreach (var order in source)
        {
            if (string.IsNullOrWhiteSpace(order.OrderUid))
            {
                continue;
            }

            if (target.ContainsKey(order.OrderUid))
            {
                continue;
            }

            target[order.OrderUid] = order;
        }
    }

    /// <summary>
    /// 建立車輛 UID 與車牌對照表，供單據快速對應車輛。
    /// </summary>
    private static (HashSet<string> CarUidSet, Dictionary<string, List<string>> PlateMap) BuildCarLookup(
        IReadOnlyCollection<CarEntity> carEntities)
    {
        var carUidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plateMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var car in carEntities)
        {
            var normalizedUid = NormalizeOptionalText(car.CarUid);
            if (normalizedUid is null)
            {
                continue;
            }

            carUidSet.Add(normalizedUid);

            var normalizedPlate = NormalizePlateKey(car.CarNo);
            if (normalizedPlate is null)
            {
                continue;
            }

            if (!plateMap.TryGetValue(normalizedPlate, out var list))
            {
                list = new List<string>();
                plateMap[normalizedPlate] = list;
            }

            if (!list.Contains(normalizedUid))
            {
                list.Add(normalizedUid);
            }
        }

        return (carUidSet, plateMap);
    }

    /// <summary>
    /// 將摘要資料加入對應車輛字典。
    /// </summary>
    private static void AppendSummary<TSummary>(
        IDictionary<string, List<TSummary>> target,
        string key,
        TSummary summary)
    {
        if (!target.TryGetValue(key, out var list))
        {
            list = new List<TSummary>();
            target[key] = list;
        }

        list.Add(summary);
    }

    /// <summary>
    /// 將摘要集合排序，統一以建立時間倒序排列，並輔以識別碼排序。
    /// </summary>
    private static void SortSummaries<TSummary>(
        IDictionary<string, List<TSummary>> target,
        Func<TSummary, DateTime?> timeSelector,
        Func<TSummary, string?> idSelector)
    {
        foreach (var key in target.Keys.ToList())
        {
            target[key] = target[key]
                .OrderByDescending(item => timeSelector(item) ?? DateTime.MinValue)
                .ThenByDescending(item => idSelector(item) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// 正規化車牌文字，轉為大寫並轉換全形字元。
    /// </summary>
    private static string NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return string.Empty;
        }

        var trimmed = plate.Trim();
        var buffer = new char[trimmed.Length];

        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];

            if (ch >= '０' && ch <= '９')
            {
                buffer[i] = (char)('0' + (ch - '０'));
                continue;
            }

            if (ch >= 'Ａ' && ch <= 'Ｚ')
            {
                buffer[i] = (char)('A' + (ch - 'Ａ'));
                continue;
            }

            if (ch >= 'ａ' && ch <= 'ｚ')
            {
                buffer[i] = (char)('A' + (ch - 'ａ'));
                continue;
            }

            buffer[i] = char.ToUpperInvariant(ch);
        }

        return new string(buffer);
    }

    /// <summary>
    /// 將車牌中的英數字取出，提供與資料庫索引欄位比對使用。
    /// </summary>
    private static string ExtractPlateKey(string plate)
    {
        var key = new string(plate.Where(char.IsLetterOrDigit).ToArray());
        return key.ToUpperInvariant();
    }

    /// <summary>
    /// 將車牌字串轉換為比對用的索引鍵，若轉換結果為空則回傳 null。
    /// </summary>
    private static string? NormalizePlateKey(string? plate)
    {
        var normalized = NormalizePlate(plate);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        var key = ExtractPlateKey(normalized);
        return string.IsNullOrEmpty(key) ? null : key;
    }

    /// <summary>
    /// 正規化可選文字欄位，移除前後空白並將空字串轉換為 null。
    /// </summary>
    private static string? NormalizeOptionalText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }

    /// <summary>
    /// 組合品牌與型號資訊，確保回傳資料具備完整的顯示欄位。
    /// </summary>
    private static (string? Brand, string? Model) ResolveCarBrandAndModel(CarEntity car)
    {
        // 先使用資料表中原始欄位，若已填寫則直接回傳。
        var brand = NormalizeOptionalText(car.Brand);
        var model = NormalizeOptionalText(car.Model);
        if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(model))
        {
            return (brand, model);
        }

        // 若原始欄位缺少資料，嘗試拆解合併欄位 BrandModel 以補足資訊。
        var brandModel = NormalizeOptionalText(car.BrandModel);
        if (string.IsNullOrEmpty(brandModel))
        {
            return (brand, model);
        }

        // 將常見分隔符號轉成空白後拆解，避免不同輸入格式造成解析失敗。
        var separators = new[] { '／', '/', '|', '｜', '-', '－' };
        var normalizedCombined = brandModel;
        foreach (var separator in separators)
        {
            normalizedCombined = normalizedCombined.Replace(separator, ' ');
        }

        var parts = normalizedCombined
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return (brand, model);
        }

        // 優先填入品牌資訊，若仍缺少型號則將剩餘文字視為型號名稱。
        if (string.IsNullOrEmpty(brand))
        {
            brand = parts[0];
        }

        if (string.IsNullOrEmpty(model) && parts.Length >= 2)
        {
            model = string.Join(' ', parts.Skip(1));
        }

        // 若品牌與型號皆為空，僅剩單一片段時，將其視為型號以避免回傳空字串。
        if (string.IsNullOrEmpty(model) && string.IsNullOrEmpty(brand))
        {
            model = parts[0];
        }

        // 當原先已有品牌，但 BrandModel 只帶入型號名稱時，需補齊型號欄位以符合前端需求。
        if (string.IsNullOrEmpty(model) && parts.Length == 1 && !string.Equals(parts[0], brand, StringComparison.OrdinalIgnoreCase))
        {
            model = parts[0];
        }

        return (brand, model);
    }
}
