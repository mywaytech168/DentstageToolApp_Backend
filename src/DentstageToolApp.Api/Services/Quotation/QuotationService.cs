using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Services.Photo;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using CarEntity = DentstageToolApp.Infrastructure.Entities.Car;
using CustomerEntity = DentstageToolApp.Infrastructure.Entities.Customer;
using StoreEntity = DentstageToolApp.Infrastructure.Entities.Store;
using TechnicianEntity = DentstageToolApp.Infrastructure.Entities.Technician;
using PhotoEntity = DentstageToolApp.Infrastructure.Entities.PhotoDatum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DentstageToolApp.Api.Services.Quotation;

/// <summary>
/// 估價單服務實作，透過資料庫查詢回傳估價單列表所需的欄位。
/// </summary>
public class QuotationService : IQuotationService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    // 序號計算時僅需少量資料即可取得最大值，限制撈取數量降低資料庫負擔。
    private const int SerialCandidateFetchCount = 50;
    // 產生測試資料時單次擷取的隨機樣本數，避免撈取過多資料造成效能負擔。
    private const int RandomCandidateFetchCount = 30;
    // 產生測試資料時使用的車體損傷位置範例。
    private static readonly string[] TestDamagePositions = { "前保桿", "後保桿", "左前門", "右後門", "引擎蓋", "車頂" };
    // 產生測試資料時使用的凹痕狀態範例。
    private static readonly string[] TestDamageStatuses = { "輕微凹痕", "中度凹痕", "需烤漆", "待確認" };
    // 產生測試資料時使用的敘述範例。
    private static readonly string[] TestDamageDescriptions = { "停車擦撞造成凹陷", "需板金搭配烤漆", "建議同時處理刮痕", "需另行評估內部結構" };
    // 產生測試資料時使用的預約方式範例，方便前端展示不同來源。
    private static readonly string[] TestBookMethodSamples = { "電話預約", "LINE 預約", "現場排程" };
    private static readonly string[] TaipeiTimeZoneIds = { "Taipei Standard Time", "Asia/Taipei" };
    private const string DefaultQuotationStatus = "110";
    private const string UnrepairableStatus = "115";
    private const string CancellationStatus = "195";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly DentstageToolAppContext _context;
    private readonly IPhotoService _photoService;
    private readonly ILogger<QuotationService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件以供查詢使用。
    /// </summary>
    public QuotationService(DentstageToolAppContext context, IPhotoService photoService, ILogger<QuotationService> logger)
    {
        _context = context;
        _photoService = photoService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateQuotationTestPageResponse> GenerateRandomQuotationTestPageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 取樣資料庫樣本 ----------
        // 為避免大量撈取資料造成效能負擔，每個實體僅擷取固定數量的樣本後再於記憶體中挑選。
        var technicianSamples = await _context.Technicians
            .AsNoTracking()
            .Include(t => t.Store)
            .Take(RandomCandidateFetchCount)
            .ToListAsync(cancellationToken);
        var customerSamples = await _context.Customers
            .AsNoTracking()
            .Take(RandomCandidateFetchCount)
            .ToListAsync(cancellationToken);
        var carSamples = await _context.Cars
            .AsNoTracking()
            .Take(RandomCandidateFetchCount)
            .ToListAsync(cancellationToken);
        var photoSamples = await _context.PhotoData
            .AsNoTracking()
            .Take(RandomCandidateFetchCount)
            .ToListAsync(cancellationToken);

        // ---------- 組裝隨機測試資料 ----------
        var random = Random.Shared;
        var technician = PickRandomOrDefault(technicianSamples, random);
        var customer = PickRandomOrDefault(customerSamples, random);
        var car = PickRandomOrDefault(carSamples, random);
        var fixTypeKey = QuotationDamageFixTypeHelper.CanonicalOrder[random.Next(QuotationDamageFixTypeHelper.CanonicalOrder.Count)];
        var fixTypeDisplayName = QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeKey);

        var reservationDate = DateTime.Now.Date
            .AddDays(random.Next(1, 8))
            .AddHours(10 + random.Next(0, 3))
            .AddMinutes(random.Next(0, 4) * 15);
        var repairDate = reservationDate
            .AddDays(random.Next(1, 5))
            .AddHours(random.Next(1, 4));

        var randomDamages = BuildRandomDamages(photoSamples, random);
        var draft = new CreateQuotationRequest
        {
            Store = new CreateQuotationStoreInfo
            {
                EstimationTechnicianUid = technician?.TechnicianUid ?? BuildFallbackUid("U"),
                CreatorTechnicianUid = technician?.TechnicianUid ?? BuildFallbackUid("U"),
                BookMethod = BuildBookMethodText(random),
                ReservationDate = reservationDate,
                RepairDate = repairDate
            },
            Car = new CreateQuotationCarInfo
            {
                CarUid = car?.CarUid ?? BuildFallbackUid("Ca"),
                BrandUid = null,
                ModelUid = null
            },
            Customer = new CreateQuotationCustomerInfo
            {
                CustomerUid = customer?.CustomerUid ?? BuildFallbackUid("Cu")
            },
            Photos = GroupDamagesForRequest(randomDamages),
            CarBodyConfirmation = BuildRandomCarBodyConfirmation(photoSamples, random),
            Maintenance = BuildRandomMaintenance(fixTypeKey, random)
        };

        var usedExistingData = technician is not null
            || customer is not null
            || car is not null
            || photoSamples.Count > 0;

        var response = new CreateQuotationTestPageResponse
        {
            Draft = draft,
            Technician = technician is null ? null : CreateTechnicianSummary(technician),
            Store = technician?.Store is null ? null : CreateStoreSummary(technician.Store),
            Customer = customer is null ? null : CreateCustomerSummary(customer),
            Car = car is null ? null : CreateCarSummary(car),
            // 維修類型僅需提供中文名稱，方便測試頁直接呈現。
            FixType = fixTypeDisplayName,
            UsedExistingData = usedExistingData,
            GeneratedAt = DateTimeOffset.Now,
            Notes = BuildTestNotes(draft, usedExistingData)
        };

        return response;
    }

    /// <inheritdoc />
    public async Task<QuotationListResponse> GetQuotationsAsync(QuotationListQuery query, CancellationToken cancellationToken)
    {
        // ---------- 查詢前置處理 ----------
        // 建立安全的分頁設定，避免前端傳入異常數值造成資料庫壓力。
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        // 將結束日期調整為當日 23:59:59，確保包含整日資料。
        DateTime? endDateInclusive = null;
        if (query.EndDate.HasValue)
        {
            endDateInclusive = query.EndDate.Value.Date.AddDays(1);
        }

        // ---------- 建立查詢 ----------
        var quotationsQuery = _context.Quatations
            .AsNoTracking()
            .AsQueryable();

        // 篩選維修類型，採用 Like 模式支援部分比對，並同步考量正規化後的中文標籤。
        if (!string.IsNullOrWhiteSpace(query.FixType))
        {
            var fixTypeFilter = query.FixType.Trim();

            // 建立查詢樣式集合，涵蓋原始輸入、拆分後的個別項目與中文顯示名稱。
            var likePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                $"%{fixTypeFilter}%"
            };

            var searchTokens = QuotationDamageFixTypeHelper.ResolveSearchTokens(fixTypeFilter);
            foreach (var token in searchTokens)
            {
                likePatterns.Add($"%{token}%");
            }

            // 將所有候選樣式整合為單一 Where 條件，確保 EF Core 可轉換為 SQL。
            var predicate = BuildFixTypeLikePredicate(likePatterns);
            quotationsQuery = quotationsQuery.Where(predicate);
        }

        // 篩選估價單狀態。
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            quotationsQuery = quotationsQuery.Where(q => q.Status == query.Status);
        }

        // 篩選建立日期（起始）。
        if (query.StartDate.HasValue)
        {
            var startDate = query.StartDate.Value.Date;
            quotationsQuery = quotationsQuery.Where(q => q.CreationTimestamp >= startDate);
        }

        // 篩選建立日期（結束，包含當日）。
        if (endDateInclusive.HasValue)
        {
            quotationsQuery = quotationsQuery.Where(q => q.CreationTimestamp < endDateInclusive.Value);
        }

        // 客戶關鍵字，模糊搜尋姓名或電話。
        if (!string.IsNullOrWhiteSpace(query.CustomerKeyword))
        {
            var keyword = query.CustomerKeyword.Trim();
            quotationsQuery = quotationsQuery.Where(q =>
                (q.Name != null && EF.Functions.Like(q.Name, $"%{keyword}%")) ||
                (q.Phone != null && EF.Functions.Like(q.Phone, $"%{keyword}%")));
        }

        // 車牌關鍵字，模糊搜尋車牌號碼。
        if (!string.IsNullOrWhiteSpace(query.CarPlateKeyword))
        {
            var plateKeyword = query.CarPlateKeyword.Trim();
            quotationsQuery = quotationsQuery.Where(q =>
                q.CarNo != null && EF.Functions.Like(q.CarNo, $"%{plateKeyword}%"));
        }

        // ---------- 計算總筆數 ----------
        var totalCount = await quotationsQuery.CountAsync(cancellationToken);

        // ---------- 套用排序與分頁 ----------
        // 使用 LEFT JOIN 連結 Brands 與 Models 主檔，優先以主檔名稱回傳品牌與車型資訊。
        // 透過多個 LEFT JOIN 串接主檔，優先取得標準化名稱供前端顯示。
        var orderedQuery =
            from quotation in quotationsQuery
            join brand in _context.Brands.AsNoTracking()
                on quotation.BrandUid equals brand.BrandUid into brandGroup
            from brand in brandGroup.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking()
                on quotation.ModelUid equals model.ModelUid into modelGroup
            from model in modelGroup.DefaultIfEmpty()
            join store in _context.Stores.AsNoTracking()
                on quotation.StoreUid equals store.StoreUid into storeGroup
            from store in storeGroup.DefaultIfEmpty()
            orderby quotation.CreationTimestamp ?? DateTime.MinValue descending,
                quotation.QuotationNo descending
            select new { quotation, brand, model, store };

        // 先取出分頁後的原始資料集合，避免一次載入過多資料。
        var pagedSource = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 彙整當前頁面所需的使用者 UID，僅針對有值的項目進行查詢，降低額外資料庫負擔。
        var estimationTechnicianUids = pagedSource
            .Select(result => NormalizeOptionalText(result.quotation.EstimationTechnicianUid))
            .Where(uid => uid is not null)
            .Select(uid => uid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var estimatorUserUids = pagedSource
            .Select(result => NormalizeOptionalText(result.quotation.UserUid))
            .Where(uid => uid is not null)
            .Select(uid => uid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var estimationTechnicianMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (estimationTechnicianUids.Count > 0)
        {
            // 優先載入估價技師主檔名稱，若找不到再回退使用者帳號顯示名稱。
            var estimatorTechnicians = await _context.Technicians
                .AsNoTracking()
                .Where(technician => estimationTechnicianUids.Contains(technician.TechnicianUid))
                .Select(technician => new { technician.TechnicianUid, technician.TechnicianName })
                .ToListAsync(cancellationToken);

            foreach (var technician in estimatorTechnicians)
            {
                var normalizedUid = NormalizeOptionalText(technician.TechnicianUid);
                var normalizedName = NormalizeOptionalText(technician.TechnicianName);
                if (normalizedUid is null || normalizedName is null)
                {
                    continue;
                }

                estimationTechnicianMap[normalizedUid] = normalizedName;
            }
        }

        if (estimatorUserUids.Count > 0)
        {
            // 針對當前頁面使用到的使用者 UID 一次撈取顯示名稱，建立快取供後續查找，減少重複查詢成本。
            var estimatorAccounts = await _context.UserAccounts
                .AsNoTracking()
                .Where(account => estimatorUserUids.Contains(account.UserUid))
                .Select(account => new { account.UserUid, account.DisplayName })
                .ToListAsync(cancellationToken);

            foreach (var account in estimatorAccounts)
            {
                // 僅保留同時具備 UID 與顯示名稱的資料，避免寫入空白映射。
                var normalizedUid = NormalizeOptionalText(account.UserUid);
                var normalizedName = NormalizeOptionalText(account.DisplayName);
                if (normalizedUid is null || normalizedName is null)
                {
                    continue;
                }

                estimationTechnicianMap[normalizedUid] = normalizedName;
            }
        }

        var creatorUserUids = pagedSource
            .Select(result => NormalizeOptionalText(result.quotation.CreatorTechnicianUid))
            .Where(uid => uid is not null)
            .Select(uid => uid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var creatorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (creatorUserUids.Count > 0)
        {
            var creatorTechnicians = await _context.Technicians
                .AsNoTracking()
                .Where(technician => creatorUserUids.Contains(technician.TechnicianUid))
                .Select(technician => new { technician.TechnicianUid, technician.TechnicianName })
                .ToListAsync(cancellationToken);

            foreach (var technician in creatorTechnicians)
            {
                var normalizedUid = NormalizeOptionalText(technician.TechnicianUid);
                var normalizedName = NormalizeOptionalText(technician.TechnicianName);
                if (normalizedUid is null || normalizedName is null)
                {
                    continue;
                }

                creatorMap[normalizedUid] = normalizedName;
            }
        }

        var items = pagedSource
            .Select(result =>
            {
                var quotation = result.quotation;
                var brand = result.brand;
                var model = result.model;
                var store = result.store;

                // 優先使用使用者帳號顯示名稱，若查無對應資料則回退為估價單上的 UserName 欄位。
                var estimatorName = quotation.UserName;
                var normalizedUserUid = NormalizeOptionalText(quotation.UserUid);
                var normalizedEstimationTechnicianUid = NormalizeOptionalText(quotation.EstimationTechnicianUid)
                    ?? normalizedUserUid;
                if (normalizedEstimationTechnicianUid is not null &&
                    estimationTechnicianMap.TryGetValue(normalizedEstimationTechnicianUid, out var mappedName) &&
                    !string.IsNullOrWhiteSpace(mappedName))
                {
                    estimatorName = mappedName;
                }

                var normalizedCreatorUid = NormalizeOptionalText(quotation.CreatorTechnicianUid);
                var creatorName = quotation.CreatedBy;
                if (normalizedCreatorUid is not null &&
                    creatorMap.TryGetValue(normalizedCreatorUid, out var mappedCreatorName) &&
                    !string.IsNullOrWhiteSpace(mappedCreatorName))
                {
                    creatorName = mappedCreatorName;
                }

                return new QuotationSummaryResponse
                {
                    QuotationNo = quotation.QuotationNo,
                    Status = quotation.Status,
                    CustomerName = quotation.Name,
                    CustomerPhone = quotation.Phone,
                    CarBrand = brand != null ? brand.BrandName : quotation.Brand,
                    CarModel = model != null ? model.ModelName : quotation.Model,
                    CarPlateNumber = quotation.CarNo,
                    EstimationTechnicianUid = normalizedEstimationTechnicianUid,
                    CreatorTechnicianUid = normalizedCreatorUid,
                    // 門市名稱優先採用主檔資料，若關聯不存在則回落至原欄位。
                    StoreName = store != null ? store.StoreName : quotation.CurrentStatusUser,
                    // 估價人員名稱若查無主檔資料，則使用估價單建立者名稱，維持舊資料相容性。
                    EstimationTechnicianName = estimatorName,
                    // 製單技師若能對應技師主檔則使用主檔名稱。
                    CreatorTechnicianName = creatorName,
                    CreatedAt = quotation.CreationTimestamp,
                    // 維修類型輸出時優先回傳中文顯示名稱，若無法解析則保留原值。
                    // 以中文標籤呈現維修類型，確保前端無需額外轉換。
                    FixType = QuotationDamageFixTypeHelper.ResolveDisplayName(quotation.FixType)
                };
            })
            .ToList();

        // ---------- 回傳結果 ----------
        return new QuotationListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<CreateQuotationResponse> CreateQuotationAsync(CreateQuotationRequest request, QuotationOperatorContext operatorContext, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價單建立資料。");
        }

        if (operatorContext is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "缺少操作人員資訊，請重新登入。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 參數整理區 ----------
        var storeInfo = request.Store ?? throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供店家資訊。");
        var carInfo = request.Car ?? throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供車輛資訊。");
        var customerInfo = request.Customer ?? throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供客戶資訊。");

        // 僅透過技師識別碼即可反查門市資料，減少前端傳遞欄位。
        var technicianEntity = await GetTechnicianEntityAsync(storeInfo.EstimationTechnicianUid, cancellationToken);
        if (technicianEntity is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請選擇有效的估價技師。");
        }

        // 透過技師關聯的門市主檔，自動補齊店鋪名稱等資訊。
        var operatorStoreUid = NormalizeOptionalText(operatorContext.StoreUid);
        var storeEntity = await GetStoreEntityAsync(operatorStoreUid, technicianEntity, cancellationToken);
        var storeUid = NormalizeRequiredText(
            operatorStoreUid
            ?? NormalizeOptionalText(storeEntity?.StoreUid)
            ?? NormalizeOptionalText(technicianEntity?.StoreUid),
            "門市識別碼");
        var storeName = NormalizeRequiredText(
            storeEntity?.StoreName
            ?? technicianEntity?.Store?.StoreName,
            "店鋪名稱");

        if (operatorStoreUid is not null && technicianEntity?.StoreUid is not null &&
            !string.Equals(operatorStoreUid, technicianEntity.StoreUid, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "登入門市 {OperatorStoreUid} 與技師所屬門市 {TechnicianStoreUid} 不一致，將以登入門市為主。",
                operatorStoreUid,
                technicianEntity.StoreUid);
        }

        var operatorLabel = NormalizeOperator(operatorContext.OperatorName);
        var estimatorUid = NormalizeRequiredText(technicianEntity.TechnicianUid, "估價技師識別碼");
        var estimatorName = NormalizeOptionalText(technicianEntity.TechnicianName) ?? operatorLabel;
        var creatorTechnicianEntity = await GetTechnicianEntityAsync(storeInfo.CreatorTechnicianUid, cancellationToken);
        var creatorUid = NormalizeOptionalText(creatorTechnicianEntity?.TechnicianUid) ?? estimatorUid;
        var creatorName = NormalizeOptionalText(creatorTechnicianEntity?.TechnicianName) ?? estimatorName;
        // 預約方式允許為空值，僅在前端提供時紀錄，利於後續統計。
        var bookMethod = NormalizeOptionalText(storeInfo.BookMethod);

        // ---------- 維修設定處理 ----------
        // 先整理維修設定欄位，維修類型若未提供將在綁定照片後自動推論。
        var maintenanceInfo = request.Maintenance ?? new CreateQuotationMaintenanceInfo();
        var reserveCarFlag = ConvertBooleanToFlag(maintenanceInfo.ReserveCar);
        var coatingFlag = ConvertBooleanToFlag(maintenanceInfo.ApplyCoating);
        var wrappingFlag = ConvertBooleanToFlag(maintenanceInfo.ApplyWrapping);
        var repaintFlag = ConvertBooleanToFlag(maintenanceInfo.HasRepainted);
        var toolFlag = ConvertBooleanToFlag(maintenanceInfo.NeedToolEvaluation);
        var maintenanceRemark = NormalizeOptionalText(maintenanceInfo.Remark);
        var legacyOtherFee = maintenanceInfo.OtherFee;
        var estimatedRepairDays = maintenanceInfo.EstimatedRepairDays;
        var estimatedRepairHours = maintenanceInfo.EstimatedRepairHours;
        var estimatedRestorationPercentage = maintenanceInfo.EstimatedRestorationPercentage;
        // FixExpect 欄位原以字串儲存修復完成度，需將百分比轉換後寫入以維持舊系統行為。
        var fixExpectText = FormatEstimatedRestorationPercentage(estimatedRestorationPercentage);
        var fixTimeHour = maintenanceInfo.FixTimeHour;
        var fixTimeMin = maintenanceInfo.FixTimeMin;
        // FixExpect_Day/Hour 需沿用前端填寫的預估工期，若舊欄位仍有資料則保留相容性。
        var fixExpectDay = maintenanceInfo.EstimatedRepairDays ?? maintenanceInfo.FixExpectDay;
        var fixExpectHour = maintenanceInfo.EstimatedRepairHours ?? maintenanceInfo.FixExpectHour;
        var suggestedPaintReason = NormalizeOptionalText(maintenanceInfo.SuggestedPaintReason);
        var unrepairableReason = NormalizeOptionalText(maintenanceInfo.UnrepairableReason);
        // 拒絕與建議鈑烤需要落在舊系統欄位，僅在有原因時才啟用旗標並填入內容。
        var rejectFlag = !string.IsNullOrEmpty(unrepairableReason);
        var panelBeatFlag = !string.IsNullOrEmpty(suggestedPaintReason);
        var roundingDiscount = maintenanceInfo.RoundingDiscount;
        var legacyPercentageDiscount = maintenanceInfo.PercentageDiscount;
        var rawDiscountReason = NormalizeOptionalText(maintenanceInfo.DiscountReason);
        var requestedCategoryAdjustments = maintenanceInfo.CategoryAdjustments;

        // ---------- 預約與維修日期處理 ----------
        // 若前端已排定預約或維修日期，需轉換為 DateOnly 以符合資料表欄位型別。
        var reservationDate = NormalizeOptionalDate(storeInfo.ReservationDate);
        var repairDate = NormalizeOptionalDate(storeInfo.RepairDate);

        // 透過車輛主檔自動帶出車牌與品牌資訊，流程僅需車輛 UID 即可，先驗證識別碼後統一補齊細節。
        var requestCarUid = NormalizeRequiredText(carInfo.CarUid, "車輛識別碼");
        var carEntity = await GetCarEntityAsync(requestCarUid, cancellationToken);
        if (carEntity is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請選擇有效的車輛資料。");
        }

        // 透過車輛主檔補齊車牌、品牌等欄位，並將車牌符號移除統一格式。
        var carUid = NormalizeRequiredText(carEntity.CarUid, "車輛識別碼");
        var originalLicensePlate = NormalizeRequiredText(carEntity.CarNo, "車牌號碼");
        // 依需求保留原始車牌的連字號供 CarNoInput 欄位使用，同時統一為大寫格式。
        var licensePlateWithSymbol = originalLicensePlate.ToUpperInvariant();
        // 系統實際使用的車牌欄位需移除連字號，方便搜尋與報表統計。
        var licensePlate = NormalizeLicensePlate(originalLicensePlate);
        var brand = NormalizeOptionalText(carEntity.Brand);
        var model = NormalizeOptionalText(carEntity.Model);
        var color = NormalizeOptionalText(carEntity.Color);
        var carRemark = NormalizeOptionalText(carEntity.CarRemark);
        var carMileage = carEntity.Milage;

        // 優先採用前端提供的品牌與車型 UID，若缺少則再依名稱回查主檔補齊。
        var brandUid = NormalizeOptionalText(carInfo.BrandUid);
        if (brandUid is null && brand is not null)
        {
            var matchedBrandUid = await _context.Brands
                .AsNoTracking()
                .Where(entity => entity.BrandName == brand)
                .Select(entity => entity.BrandUid)
                .FirstOrDefaultAsync(cancellationToken);

            brandUid = NormalizeOptionalText(matchedBrandUid);
        }

        var modelUid = NormalizeOptionalText(carInfo.ModelUid);
        if (modelUid is null && model is not null)
        {
            var modelQuery = _context.Models
                .AsNoTracking()
                .Where(entity => entity.ModelName == model);

            if (brandUid is not null)
            {
                modelQuery = modelQuery.Where(entity => entity.BrandUid == brandUid);
            }

            var matchedModelUid = await modelQuery
                .Select(entity => entity.ModelUid)
                .FirstOrDefaultAsync(cancellationToken);

            modelUid = NormalizeOptionalText(matchedModelUid);
        }

        if (carInfo.Mileage.HasValue)
        {
            // 若前端提供即時里程數，優先覆寫車輛主檔的舊資料。
            carMileage = carInfo.Mileage;
        }

        // 透過客戶主檔自動帶出姓名與聯絡資訊，讓前端僅需傳遞 UID 即可完成建檔。
        var requestCustomerUid = NormalizeRequiredText(customerInfo.CustomerUid, "客戶識別碼");
        var customerEntity = await GetCustomerEntityAsync(requestCustomerUid, cancellationToken);
        if (customerEntity is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請選擇有效的客戶資料。");
        }

        // 透過客戶主檔補齊姓名、聯絡電話等欄位，確保僅憑 UID 即可完成建檔。
        var customerUid = NormalizeRequiredText(customerEntity.CustomerUid, "客戶識別碼");
        var customerName = NormalizeRequiredText(customerEntity.Name, "客戶名稱");
        var customerPhone = NormalizeOptionalText(customerEntity.Phone);
        var customerGender = NormalizeOptionalText(customerEntity.Gender);
        var customerType = NormalizeOptionalText(customerEntity.CustomerType);
        var customerCounty = NormalizeOptionalText(customerEntity.County);
        var customerTownship = NormalizeOptionalText(customerEntity.Township);
        var customerReason = NormalizeOptionalText(customerEntity.Reason);
        var customerSource = NormalizeOptionalText(customerEntity.Source);
        var customerRemark = NormalizeOptionalText(customerEntity.ConnectRemark);
        var customerEmail = NormalizeOptionalText(customerEntity.Email);

        var normalizedDamages = ExtractDamageList(request);
        var carBodyConfirmation = request.CarBodyConfirmation;
        var photoUids = CollectPhotoUids(normalizedDamages, carBodyConfirmation);

        if (photoUids.Count > 0)
        {
            await EnsurePhotosAvailableForCreationAsync(photoUids, cancellationToken);
            await PopulateDamageFixTypesAsync(photoUids, normalizedDamages, cancellationToken);
        }
        else
        {
            foreach (var damage in normalizedDamages)
            {
                QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
            }
        }

        var fixTypeDisplayName = DetermineOverallFixType(normalizedDamages);

        // 建立日期改由系統產生，減少前端填寫欄位。
        var createdAt = GetTaipeiNow();
        var quotationDate = DateOnly.FromDateTime(createdAt);
        var phoneQuery = NormalizePhoneQuery(customerPhone);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 系統資料計算區 ----------
        var serialNumber = await GenerateNextSerialNumberAsync(createdAt, cancellationToken);
        var quotationUid = BuildQuotationUid();
        var quotationNo = BuildQuotationNo(serialNumber, createdAt);

        // remark 改以包裝 JSON 儲存傷痕、簽名與折扣資訊，仍保留純文字備註於 PlainRemark。
        var hasExplicitCategoryAdjustments = HasCategoryAdjustments(requestedCategoryAdjustments);
        var primaryFixType = ExtractPrimaryQuotationFixType(fixTypeDisplayName);
        var fallbackCategoryKey = ResolveCategoryKeyFromFixType(primaryFixType);
        var preferBeautyAlias = string.Equals(fallbackCategoryKey, "beauty", StringComparison.OrdinalIgnoreCase);
        var financials = CalculateMaintenanceFinancialSummary(
            normalizedDamages,
            legacyOtherFee,
            roundingDiscount,
            legacyPercentageDiscount,
            rawDiscountReason,
            requestedCategoryAdjustments,
            hasExplicitCategoryAdjustments,
            fallbackCategoryKey,
            preferBeautyAlias);
        var otherFee = financials.OtherFee;
        var percentageDiscount = financials.EffectivePercentageDiscount;
        var discountReason = financials.DiscountReason ?? rawDiscountReason;
        var categoryAdjustmentsForStorage = financials.HasAdjustmentData ? financials.Adjustments : null;
        var categoryAdjustmentsForExtra = financials.HasExplicitAdjustments ? financials.Adjustments : null;
        var valuation = financials.Valuation;

        var extraData = BuildExtraData(
            normalizedDamages,
            carBodyConfirmation,
            otherFee,
            roundingDiscount,
            percentageDiscount,
            discountReason,
            estimatedRepairDays,
            estimatedRepairHours,
            estimatedRestorationPercentage,
            suggestedPaintReason,
            unrepairableReason,
            categoryAdjustmentsForExtra);
        var remarkPayload = SerializeRemark(maintenanceRemark, extraData);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 實體建立區 ----------
        var quotationEntity = new Quatation
        {
            QuotationUid = quotationUid,
            QuotationNo = quotationNo,
            SerialNum = serialNumber,
            CreationTimestamp = createdAt,
            CreatedBy = creatorName,
            ModificationTimestamp = createdAt,
            ModifiedBy = operatorLabel,
            UserUid = estimatorUid,
            CreatorTechnicianUid = creatorUid,
            Date = quotationDate,
            StoreUid = storeUid,
            EstimationTechnicianUid = estimatorUid,
            UserName = estimatorName,
            BookDate = reservationDate,
            BookMethod = bookMethod,
            FixDate = repairDate,
            FixType = fixTypeDisplayName,
            CarReserved = reserveCarFlag,
            Coat = coatingFlag,
            Envelope = wrappingFlag,
            Paint = repaintFlag,
            ToolTest = toolFlag,
            CarUid = carUid,
            CarNo = licensePlate,
            CarNoInput = licensePlateWithSymbol,
            CarNoInputGlobal = licensePlateWithSymbol,
            Brand = brand,
            Model = model,
            BrandUid = brandUid,
            ModelUid = modelUid,
            BrandModel = BuildBrandModel(brand, model),
            Color = color,
            CarRemark = carRemark,
            Milage = carMileage,
            CustomerUid = customerUid,
            Name = customerName,
            Phone = customerPhone,
            PhoneInput = customerPhone,
            PhoneInputGlobal = phoneQuery ?? customerPhone,
            Gender = customerGender,
            // 客戶屬性與聯絡地址改由顧客主檔帶入，確保估價單與客戶資訊一致。
            CustomerType = customerType,
            County = customerCounty,
            Township = customerTownship,
            Reason = customerReason,
            ConnectRemark = customerRemark,
            // 消息來源改由客戶主檔統一帶入，避免前端重複傳遞同一資料。
            Source = customerSource,
            Email = customerEmail,
            Remark = remarkPayload,
            Discount = roundingDiscount,
            DiscountPercent = percentageDiscount,
            DiscountReason = discountReason,
            Valuation = valuation,
            FixTimeHour = fixTimeHour,
            FixTimeMin = fixTimeMin,
            FixExpect = fixExpectText,
            FixExpectDay = fixExpectDay,
            FixExpectHour = fixExpectHour,
            // 拒絕欄位以布林記錄，資料庫會自動轉換為 tinyint(1)。
            Reject = rejectFlag ? true : null,
            RejectReason = rejectFlag ? unrepairableReason : null,
            // PanelBeat 仍採用 1/空白 字串以符合舊系統慣例。
            PanelBeat = panelBeatFlag ? "1" : null,
            PanelBeatReason = panelBeatFlag ? suggestedPaintReason : null
        };

        // ---------- 類別折扣欄位同步 ----------
        ApplyCategoryAdjustments(quotationEntity, categoryAdjustmentsForStorage);

        // ---------- 狀態初始化 ----------
        // 建立估價單時若填寫不能維修原因，直接套用 115 不能維修狀態，避免落入取消流程。
        var initialStatus = rejectFlag ? UnrepairableStatus : DefaultQuotationStatus;
        ApplyStatusAudit(quotationEntity, initialStatus, operatorLabel, createdAt);

        if (!rejectFlag)
        {
            // 預設估價中的狀態操作人維持門市名稱，沿用舊系統顯示方式。
            quotationEntity.Status110User = storeName;
            quotationEntity.CurrentStatusUser = storeName;
        }

        await _context.Quatations.AddAsync(quotationEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // 將建立流程中帶入的所有 PhotoUID 一次綁定到新建立的估價單。
        if (photoUids.Count > 0)
        {
            // 若圖片已綁定其他估價單則禁止重複使用，確保圖片歸屬唯一。
            await _photoService.BindToQuotationAsync(quotationEntity.QuotationUid, photoUids, cancellationToken);
        }

        await SyncDamagePhotoMetadataAsync(normalizedDamages, carBodyConfirmation?.SignaturePhotoUid, cancellationToken);
        await MarkSignaturePhotoAsync(carBodyConfirmation?.SignaturePhotoUid, cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 建立估價單 {QuotationUid} ({QuotationNo}) 成功。", operatorLabel, quotationEntity.QuotationUid, quotationEntity.QuotationNo);

        return new CreateQuotationResponse
        {
            QuotationUid = quotationEntity.QuotationUid,
            QuotationNo = quotationEntity.QuotationNo,
            CreatedAt = quotationEntity.CreationTimestamp ?? createdAt
        };
    }

    /// <inheritdoc />
    public async Task<QuotationDetailResponse> GetQuotationAsync(GetQuotationRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價單查詢條件。");
        }

        // 估價單明細頁改以編號為唯一依據，因此在進行查詢前先正規化並檢核是否有帶入資料。
        var quotationNo = NormalizeOptionalText(request.QuotationNo);
        if (quotationNo is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價單編號。");
        }

        // 以 IQueryable 宣告以保留 Include 結果的延伸查詢，同時方便套用後續條件。
        IQueryable<Quatation> query = _context.Quatations
            .AsNoTracking()
            .Include(q => q.StoreNavigation)
            .Include(q => q.BrandNavigation)
            .Include(q => q.ModelNavigation);

        // 估價單編號過濾邏輯與 Include 不衝突，因此直接回寫 IQueryable 以避免轉型例外。
        query = ApplyQuotationFilter(query, quotationNo);

        var quotation = await query.FirstOrDefaultAsync(cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無符合條件的估價單。");
        }

        var (plainRemark, extraData) = ParseRemark(quotation.Remark);

        // ---------- 估價人員名稱組裝 ----------
        // 預設使用估價單上紀錄的 UserName，若估價技師 UID 能對應技師主檔則優先採用主檔名稱。
        var estimationTechnicianUid = NormalizeOptionalText(quotation.EstimationTechnicianUid)
            ?? NormalizeOptionalText(quotation.UserUid);
        var estimationTechnicianName = quotation.UserName;
        if (!string.IsNullOrWhiteSpace(quotation.EstimationTechnicianUid))
        {
            var technicianDisplayName = await _context.Technicians
                .AsNoTracking()
                .Where(technician => technician.TechnicianUid == quotation.EstimationTechnicianUid)
                .Select(technician => technician.TechnicianName)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(technicianDisplayName))
            {
                estimationTechnicianName = technicianDisplayName;
            }
        }

        if (string.IsNullOrWhiteSpace(estimationTechnicianName) && !string.IsNullOrWhiteSpace(quotation.UserUid))
        {
            // 若估價技師主檔未能查得名稱則回退使用者帳號顯示名稱，維持相容。
            var accountDisplayName = await _context.UserAccounts
                .AsNoTracking()
                .Where(account => account.UserUid == quotation.UserUid)
                .Select(account => account.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(accountDisplayName))
            {
                estimationTechnicianName = accountDisplayName;
            }
        }

        if (string.IsNullOrWhiteSpace(estimationTechnicianName))
        {
            estimationTechnicianName = quotation.UserName;
        }

        var creatorUid = NormalizeOptionalText(quotation.CreatorTechnicianUid);
        var creatorName = quotation.CreatedBy;
        if (creatorUid is not null)
        {
            var creatorTechnicianName = await _context.Technicians
                .AsNoTracking()
                .Where(technician => technician.TechnicianUid == creatorUid)
                .Select(technician => technician.TechnicianName)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(creatorTechnicianName))
            {
                creatorName = creatorTechnicianName;
            }
        }

        var damages = extraData?.Damages;
        if ((damages is null || damages.Count == 0) && extraData?.ServiceCategories is not null)
        {
            var legacyDamages = FlattenCategoryDamages(extraData.ServiceCategories);
            if (legacyDamages.Count > 0)
            {
                damages = legacyDamages;
            }
        }

        var normalizedDamages = damages ?? new List<QuotationDamageItem>();
        normalizedDamages = await NormalizeDamagesWithPhotoDataAsync(
            quotation.QuotationUid,
            normalizedDamages,
            extraData?.CarBodyConfirmation?.SignaturePhotoUid,
            cancellationToken);
        // 依前端需求重新整理傷痕資料，只保留核心欄位並挑選主要照片。
        var photoSummaries = BuildPhotoSummaryCollection(normalizedDamages);
        // 移除多餘欄位的車體確認單內容，僅保留必要資訊。
        var simplifiedCarBody = SimplifyCarBodyConfirmation(extraData?.CarBodyConfirmation);
        var maintenanceRemark = plainRemark;
        var roundingDiscount = extraData?.RoundingDiscount ?? quotation.Discount;
        var storedOtherFee = extraData?.OtherFee;
        var storedPercentageDiscount = extraData?.PercentageDiscount ?? quotation.DiscountPercent;
        var storedDiscountReason = NormalizeOptionalText(extraData?.DiscountReason ?? quotation.DiscountReason);
        // ---------- 類別折扣欄位合併 ----------
        // 透過合併 remark JSON 與資料庫欄位的資料，確保新寫入欄位的折扣資料能在詳情頁顯示，同時保留舊資料的相容性。
        var columnCategoryAdjustments = ExtractCategoryAdjustments(quotation);
        var mergedCategoryAdjustments = MergeCategoryAdjustments(extraData?.CategoryAdjustments, columnCategoryAdjustments);
        var hasExplicitCategoryAdjustments = HasCategoryAdjustments(extraData?.CategoryAdjustments);
        var aggregatedFixType = string.IsNullOrWhiteSpace(quotation.FixType)
            ? DetermineOverallFixType(normalizedDamages)
            : quotation.FixType;
        var primaryFixType = ExtractPrimaryQuotationFixType(aggregatedFixType);
        var fallbackCategoryKey = ResolveCategoryKeyFromFixType(primaryFixType);
        var preferBeautyAlias = string.Equals(fallbackCategoryKey, "beauty", StringComparison.OrdinalIgnoreCase);
        var financials = CalculateMaintenanceFinancialSummary(
            normalizedDamages,
            storedOtherFee,
            roundingDiscount,
            storedPercentageDiscount,
            storedDiscountReason,
            mergedCategoryAdjustments,
            hasExplicitCategoryAdjustments,
            fallbackCategoryKey,
            preferBeautyAlias);
        var otherFee = financials.OtherFee ?? storedOtherFee;
        var percentageDiscount = financials.EffectivePercentageDiscount ?? storedPercentageDiscount;
        var discountReason = NormalizeOptionalText(financials.DiscountReason ?? storedDiscountReason);
        // 舊資料需依維修類型帶出對應欄位，因此只要存在資料即回傳分類結構。
        QuotationMaintenanceCategoryAdjustmentCollection? categoryAdjustments = null;
        if (financials.HasAdjustmentData)
        {
            categoryAdjustments = CloneCategoryAdjustments(financials.Adjustments);
        }
        // 回傳時優先採用舊系統欄位，若舊資料仍存於 remark JSON 中則保留相容性。
        var estimatedRepairDays = quotation.FixExpectDay ?? extraData?.EstimatedRepairDays;
        var estimatedRepairHours = quotation.FixExpectHour ?? extraData?.EstimatedRepairHours;
        // 修復完成度若尚未寫入 remark JSON，需從 FixExpect 字串回推數值供前端使用。
        var estimatedRestorationPercentage = extraData?.EstimatedRestorationPercentage
            ?? ParseEstimatedRestorationPercentage(quotation.FixExpect);
        var suggestedPaintReason = NormalizeOptionalText(quotation.PanelBeatReason)
            ?? NormalizeOptionalText(extraData?.SuggestedPaintReason);
        var unrepairableReason = NormalizeOptionalText(quotation.RejectReason)
            ?? NormalizeOptionalText(extraData?.UnrepairableReason);

        // 透過現有估價金額與折扣計算應付金額，避免直接存取缺少對應欄位的實體屬性。
        var amount = CalculateOrderAmount(quotation.Valuation, quotation.Discount);

        return new QuotationDetailResponse
        {
            QuotationUid = quotation.QuotationUid,
            QuotationNo = quotation.QuotationNo,
            Status = quotation.Status,
            CreatedAt = quotation.CreationTimestamp,
            UpdatedAt = quotation.ModificationTimestamp,
            Amounts = new QuotationAmountInfo
            {
                // 金額欄位需同步維修單顯示，避免前端計算不一致。
                Valuation = quotation.Valuation,
                Discount = quotation.Discount,
                DiscountPercent = quotation.DiscountPercent,
                // Quatation 實體未直接提供 Amount 欄位，因此統一由服務層計算後回傳。
                Amount = amount
            },
            Store = new QuotationStoreInfo
            {
                StoreUid = quotation.StoreUid,
                UserUid = quotation.UserUid,
                CreatorTechnicianUid = creatorUid,
                StoreName = quotation.StoreNavigation?.StoreName ?? quotation.CurrentStatusUser ?? string.Empty,
                // 估價人員名稱優先顯示使用者主檔資料，若查無對應使用者則回退為建立者姓名。
                EstimationTechnicianName = estimationTechnicianName,
                CreatorTechnicianName = creatorName,
                // 估價技師識別碼需與建立估價單時相同，方便前端直接帶入技師選項。
                EstimationTechnicianUid = NormalizeOptionalText(quotation.EstimationTechnicianUid)
                    ?? estimationTechnicianUid,
                CreatedDate = quotation.CreationTimestamp,
                ReservationDate = quotation.BookDate?.ToDateTime(TimeOnly.MinValue),
                BookMethod = quotation.BookMethod,
                RepairDate = quotation.FixDate?.ToDateTime(TimeOnly.MinValue)
            },
            Car = new QuotationCarInfo
            {
                CarUid = quotation.CarUid,
                LicensePlate = quotation.CarNo,
                Brand = quotation.BrandNavigation?.BrandName ?? quotation.Brand,
                Model = quotation.ModelNavigation?.ModelName ?? quotation.Model,
                BrandUid = quotation.BrandUid,
                ModelUid = quotation.ModelUid,
                Color = quotation.Color,
                Remark = quotation.CarRemark,
                Mileage = quotation.Milage
            },
            Customer = new QuotationCustomerInfo
            {
                CustomerUid = quotation.CustomerUid,
                Name = quotation.Name,
                Phone = quotation.Phone,
                Email = quotation.Email,
                Gender = quotation.Gender,
                CustomerType = quotation.CustomerType,
                County = quotation.County,
                Township = quotation.Township,
                Reason = quotation.Reason,
                Source = quotation.Source,
                Remark = quotation.ConnectRemark
            },
            Photos = photoSummaries,
            CarBodyConfirmation = simplifiedCarBody,
            Maintenance = new QuotationMaintenanceDetail
            {
                ReserveCar = ParseBooleanFlag(quotation.CarReserved),
                ApplyCoating = ParseBooleanFlag(quotation.Coat),
                ApplyWrapping = ParseBooleanFlag(quotation.Envelope),
                HasRepainted = ParseBooleanFlag(quotation.Paint),
                NeedToolEvaluation = ParseBooleanFlag(quotation.ToolTest),
                Remark = maintenanceRemark,
                OtherFee = otherFee,
                RoundingDiscount = roundingDiscount,
                PercentageDiscount = percentageDiscount,
                DiscountReason = discountReason,
                CategoryAdjustments = categoryAdjustments,
                EstimatedRepairDays = estimatedRepairDays,
                EstimatedRepairHours = estimatedRepairHours,
                EstimatedRestorationPercentage = estimatedRestorationPercentage,
                SuggestedPaintReason = suggestedPaintReason,
                UnrepairableReason = unrepairableReason
            }
        };
    }

    /// <inheritdoc />
    public async Task UpdateQuotationAsync(UpdateQuotationRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價單更新資料。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var quotationNo = EnsureRequestHasQuotationNo(request);
        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無需更新的估價單。");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var carInfo = request.Car ?? new QuotationCarInfo();
        var customerInfo = request.Customer ?? new QuotationCustomerInfo();
        var requestedDamages = request.Photos?.ToDamageList() ?? new List<QuotationDamageItem>();
        var (plainRemark, existingExtra) = ParseRemark(quotation.Remark);
        var maintenanceInfo = request.Maintenance;
        var storeInfo = request.Store;
        DateOnly? requestedReservationDate = null;
        DateOnly? requestedRepairDate = null;
        var now = GetTaipeiNow();

        // ---------- 車輛資料同步 ----------
        if (carInfo.LicensePlate is not null)
        {
            var normalizedPlate = NormalizeOptionalText(carInfo.LicensePlate);
            if (!string.IsNullOrWhiteSpace(normalizedPlate))
            {
                var plateWithSymbol = normalizedPlate.ToUpperInvariant();
                quotation.CarNo = NormalizeLicensePlate(normalizedPlate);
                quotation.CarNoInput = plateWithSymbol;
                quotation.CarNoInputGlobal = plateWithSymbol;
            }
        }

        if (carInfo.Brand is not null)
        {
            quotation.Brand = NormalizeOptionalText(carInfo.Brand);
        }

        if (carInfo.Model is not null)
        {
            quotation.Model = NormalizeOptionalText(carInfo.Model);
        }

        if (carInfo.BrandUid is not null)
        {
            quotation.BrandUid = NormalizeOptionalText(carInfo.BrandUid);
        }

        if (carInfo.ModelUid is not null)
        {
            quotation.ModelUid = NormalizeOptionalText(carInfo.ModelUid);
        }

        quotation.BrandModel = BuildBrandModel(quotation.Brand, quotation.Model);

        if (carInfo.Color is not null)
        {
            quotation.Color = NormalizeOptionalText(carInfo.Color);
        }

        if (carInfo.Remark is not null)
        {
            quotation.CarRemark = NormalizeOptionalText(carInfo.Remark);
        }

        if (carInfo.Mileage.HasValue)
        {
            // 里程數以最新值覆寫，維持估價與維修流程可共用同一筆數據。
            quotation.Milage = carInfo.Mileage;
        }

        // ---------- 客戶資料同步 ----------
        quotation.Name = NormalizeOptionalText(customerInfo.Name) ?? quotation.Name;

        if (customerInfo.Phone is not null)
        {
            quotation.Phone = NormalizeOptionalText(customerInfo.Phone);
            quotation.PhoneInput = quotation.Phone;
            quotation.PhoneInputGlobal = quotation.Phone;
        }

        if (customerInfo.Email is not null)
        {
            // 若前端補齊電子郵件，優先以最新資料覆寫估價單欄位供後續聯繫。
            quotation.Email = NormalizeOptionalText(customerInfo.Email);
        }

        if (customerInfo.Gender is not null)
        {
            quotation.Gender = NormalizeOptionalText(customerInfo.Gender);
        }

        if (customerInfo.CustomerType is not null)
        {
            quotation.CustomerType = NormalizeOptionalText(customerInfo.CustomerType);
        }

        if (customerInfo.County is not null)
        {
            quotation.County = NormalizeOptionalText(customerInfo.County);
        }

        if (customerInfo.Township is not null)
        {
            quotation.Township = NormalizeOptionalText(customerInfo.Township);
        }

        if (customerInfo.Reason is not null)
        {
            quotation.Reason = NormalizeOptionalText(customerInfo.Reason);
        }

        if (customerInfo.Source is not null)
        {
            quotation.Source = NormalizeOptionalText(customerInfo.Source);
        }

        if (customerInfo.Remark is not null)
        {
            quotation.ConnectRemark = NormalizeOptionalText(customerInfo.Remark);
        }

        // ---------- 店家與排程資料同步 ----------
        if (storeInfo is not null)
        {
            if (storeInfo.BookMethod is not null)
            {
                // 僅在前端提供預約方式時更新，避免覆寫舊資料。
                quotation.BookMethod = NormalizeOptionalText(storeInfo.BookMethod);
            }

            // 需要改派技師時同步更新估價單的技師 UID 與顯示名稱。
            if (!string.IsNullOrWhiteSpace(storeInfo.EstimationTechnicianUid))
            {
                var normalizedTechnicianUid = NormalizeOptionalText(storeInfo.EstimationTechnicianUid);

                if (!string.IsNullOrWhiteSpace(normalizedTechnicianUid) &&
                    !string.Equals(normalizedTechnicianUid, quotation.EstimationTechnicianUid, StringComparison.OrdinalIgnoreCase))
                {
                    // 僅在識別碼改變時查詢主檔，避免每次更新都造訪資料庫。
                    var technicianEntity = await GetTechnicianEntityAsync(normalizedTechnicianUid, cancellationToken);
                    if (technicianEntity is null)
                    {
                        throw new QuotationManagementException(HttpStatusCode.BadRequest, "請選擇有效的估價技師。");
                    }

                    var technicianUid = NormalizeOptionalText(technicianEntity.TechnicianUid);
                    quotation.EstimationTechnicianUid = technicianUid;
                    quotation.UserUid = technicianUid ?? quotation.UserUid;
                    quotation.UserName = NormalizeOptionalText(technicianEntity.TechnicianName) ?? quotation.UserName;

                    // 技師所屬門市若存在，連動更新 StoreUid 供報表使用。
                    var technicianStoreUid = NormalizeOptionalText(technicianEntity.StoreUid);
                    if (technicianStoreUid is not null)
                    {
                        quotation.StoreUid = technicianStoreUid;
                    }
                }
            }

            if (storeInfo.CreatorTechnicianUid is not null)
            {
                var normalizedCreatorUid = NormalizeOptionalText(storeInfo.CreatorTechnicianUid);
                if (!string.IsNullOrWhiteSpace(normalizedCreatorUid) &&
                    !string.Equals(normalizedCreatorUid, quotation.CreatorTechnicianUid, StringComparison.OrdinalIgnoreCase))
                {
                    var creatorTechnicianEntity = await GetTechnicianEntityAsync(normalizedCreatorUid, cancellationToken);
                    if (creatorTechnicianEntity is null)
                    {
                        throw new QuotationManagementException(HttpStatusCode.BadRequest, "請選擇有效的製單技師。");
                    }

                    quotation.CreatorTechnicianUid = NormalizeOptionalText(creatorTechnicianEntity.TechnicianUid);
                    quotation.CreatedBy = NormalizeOptionalText(creatorTechnicianEntity.TechnicianName)
                        ?? quotation.CreatedBy;
                }
            }

            // 僅在外部提供日期時才更新，避免覆蓋原有排程。
            if (storeInfo.ReservationDate.HasValue)
            {
                requestedReservationDate = NormalizeOptionalDate(storeInfo.ReservationDate);
            }

            if (storeInfo.RepairDate.HasValue)
            {
                requestedRepairDate = NormalizeOptionalDate(storeInfo.RepairDate);
            }
        }

        // ---------- 傷痕、簽名與維修資訊同步 ----------
        var effectiveDamages = requestedDamages.Count > 0
            ? requestedDamages
            : existingExtra?.Damages ?? new List<QuotationDamageItem>();
        var carBodyConfirmation = request.CarBodyConfirmation ?? existingExtra?.CarBodyConfirmation;
        var photoUids = CollectPhotoUids(effectiveDamages, carBodyConfirmation);

        if (photoUids.Count > 0)
        {
            await PopulateDamageFixTypesAsync(photoUids, effectiveDamages, cancellationToken);
        }
        else
        {
            foreach (var damage in effectiveDamages)
            {
                QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
            }
        }

        var resolvedFixType = DetermineOverallFixType(effectiveDamages);
        if (resolvedFixType is not null)
        {
            quotation.FixType = resolvedFixType;
        }

        var maintenanceRemark = plainRemark;
        decimal? legacyOtherFee = existingExtra?.OtherFee;
        decimal? roundingDiscount = quotation.Discount ?? existingExtra?.RoundingDiscount;
        decimal? legacyPercentageDiscount = quotation.DiscountPercent ?? existingExtra?.PercentageDiscount;
        var rawDiscountReason = NormalizeOptionalText(existingExtra?.DiscountReason ?? quotation.DiscountReason);
        var columnCategoryAdjustments = ExtractCategoryAdjustments(quotation);
        var storedCategoryAdjustments = MergeCategoryAdjustments(existingExtra?.CategoryAdjustments, columnCategoryAdjustments);
        var requestedCategoryAdjustments = CloneCategoryAdjustments(storedCategoryAdjustments);
        if (existingExtra is not null)
        {
            existingExtra.CategoryAdjustments = storedCategoryAdjustments;
        }
        var estimatedRepairDays = quotation.FixExpectDay ?? existingExtra?.EstimatedRepairDays;
        var estimatedRepairHours = quotation.FixExpectHour ?? existingExtra?.EstimatedRepairHours;
        var estimatedRestorationPercentage = existingExtra?.EstimatedRestorationPercentage
            ?? ParseEstimatedRestorationPercentage(quotation.FixExpect);
        var fixTimeHour = quotation.FixTimeHour;
        var fixTimeMin = quotation.FixTimeMin;
        var fixExpectDay = quotation.FixExpectDay ?? estimatedRepairDays;
        var fixExpectHour = quotation.FixExpectHour ?? estimatedRepairHours;
        var suggestedPaintReason = NormalizeOptionalText(existingExtra?.SuggestedPaintReason ?? quotation.PanelBeatReason);
        var unrepairableReason = NormalizeOptionalText(existingExtra?.UnrepairableReason ?? quotation.RejectReason);

        if (maintenanceInfo is not null)
        {
            maintenanceRemark = NormalizeOptionalText(maintenanceInfo.Remark) ?? maintenanceRemark;

            if (maintenanceInfo.ReserveCar.HasValue)
            {
                quotation.CarReserved = ConvertBooleanToFlag(maintenanceInfo.ReserveCar);
            }

            if (maintenanceInfo.ApplyCoating.HasValue)
            {
                quotation.Coat = ConvertBooleanToFlag(maintenanceInfo.ApplyCoating);
            }

            if (maintenanceInfo.ApplyWrapping.HasValue)
            {
                quotation.Envelope = ConvertBooleanToFlag(maintenanceInfo.ApplyWrapping);
            }

            if (maintenanceInfo.HasRepainted.HasValue)
            {
                quotation.Paint = ConvertBooleanToFlag(maintenanceInfo.HasRepainted);
            }

            if (maintenanceInfo.NeedToolEvaluation.HasValue)
            {
                quotation.ToolTest = ConvertBooleanToFlag(maintenanceInfo.NeedToolEvaluation);
            }

            if (maintenanceInfo.RoundingDiscount.HasValue)
            {
                roundingDiscount = maintenanceInfo.RoundingDiscount;
            }

            if (maintenanceInfo.OtherFee.HasValue)
            {
                legacyOtherFee = maintenanceInfo.OtherFee;
            }

            if (maintenanceInfo.PercentageDiscount.HasValue)
            {
                legacyPercentageDiscount = maintenanceInfo.PercentageDiscount;
            }

            if (maintenanceInfo.DiscountReason is not null)
            {
                rawDiscountReason = NormalizeOptionalText(maintenanceInfo.DiscountReason);
            }

            if (maintenanceInfo.CategoryAdjustments is not null)
            {
                requestedCategoryAdjustments = CloneCategoryAdjustments(maintenanceInfo.CategoryAdjustments);
            }

            if (maintenanceInfo.EstimatedRepairDays.HasValue)
            {
                estimatedRepairDays = maintenanceInfo.EstimatedRepairDays;
                fixExpectDay = maintenanceInfo.EstimatedRepairDays;
            }

            if (maintenanceInfo.EstimatedRepairHours.HasValue)
            {
                estimatedRepairHours = maintenanceInfo.EstimatedRepairHours;
                fixExpectHour = maintenanceInfo.EstimatedRepairHours;
            }

            if (maintenanceInfo.EstimatedRestorationPercentage.HasValue)
            {
                estimatedRestorationPercentage = maintenanceInfo.EstimatedRestorationPercentage;
            }

            if (maintenanceInfo.FixTimeHour.HasValue)
            {
                fixTimeHour = maintenanceInfo.FixTimeHour;
            }

            if (maintenanceInfo.FixTimeMin.HasValue)
            {
                fixTimeMin = maintenanceInfo.FixTimeMin;
            }

            if (maintenanceInfo.FixExpectDay.HasValue)
            {
                fixExpectDay = maintenanceInfo.FixExpectDay;
            }

            if (maintenanceInfo.FixExpectHour.HasValue)
            {
                fixExpectHour = maintenanceInfo.FixExpectHour;
            }

            if (maintenanceInfo.SuggestedPaintReason is not null)
            {
                suggestedPaintReason = NormalizeOptionalText(maintenanceInfo.SuggestedPaintReason);
            }

            if (maintenanceInfo.UnrepairableReason is not null)
            {
                unrepairableReason = NormalizeOptionalText(maintenanceInfo.UnrepairableReason);
            }
        }

        var hasRequestedCategoryAdjustments = HasCategoryAdjustments(requestedCategoryAdjustments);
        var primaryFixType = ExtractPrimaryQuotationFixType(resolvedFixType ?? quotation.FixType);
        var fallbackCategoryKey = ResolveCategoryKeyFromFixType(primaryFixType);
        var preferBeautyAlias = string.Equals(fallbackCategoryKey, "beauty", StringComparison.OrdinalIgnoreCase);
        var financials = CalculateMaintenanceFinancialSummary(
            effectiveDamages,
            legacyOtherFee,
            roundingDiscount,
            legacyPercentageDiscount,
            rawDiscountReason,
            requestedCategoryAdjustments,
            hasRequestedCategoryAdjustments,
            fallbackCategoryKey,
            preferBeautyAlias);
        var otherFee = financials.OtherFee;
        var percentageDiscount = financials.EffectivePercentageDiscount;
        var discountReason = financials.DiscountReason ?? rawDiscountReason;
        var categoryAdjustmentsForStorage = financials.HasAdjustmentData ? financials.Adjustments : null;
        var categoryAdjustmentsForExtra = financials.HasExplicitAdjustments ? financials.Adjustments : null;
        var valuation = financials.Valuation;

        quotation.Discount = roundingDiscount;
        quotation.DiscountPercent = percentageDiscount;
        quotation.DiscountReason = discountReason;
        ApplyCategoryAdjustments(quotation, categoryAdjustmentsForStorage);
        quotation.FixTimeHour = fixTimeHour;
        quotation.FixTimeMin = fixTimeMin;
        quotation.FixExpectDay = fixExpectDay;
        quotation.FixExpectHour = fixExpectHour;
        quotation.FixExpect = FormatEstimatedRestorationPercentage(estimatedRestorationPercentage);

        if (requestedReservationDate.HasValue)
        {
            // 預約日期需儲存為 DateOnly，維持資料庫欄位型別一致。
            quotation.BookDate = requestedReservationDate;
        }

        if (requestedRepairDate.HasValue)
        {
            quotation.FixDate = requestedRepairDate;
        }

        var rejectFlag = !string.IsNullOrEmpty(unrepairableReason);

        // ---------- 不能維修與取消狀態稽核 ----------
        // 若維修資訊包含不能維修原因，直接套用 115 不能維修狀態，不再走 195 取消流程。
        if (rejectFlag)
        {
            ClearCancellationAudit(quotation);
            ApplyStatusAudit(quotation, UnrepairableStatus, operatorLabel, now);
        }
        else if (IsUnrepairableStatus(quotation.Status))
        {
            // 解除不能維修原因時，優先回復到歷史狀態，若缺少紀錄則回到預設 110。
            var fallbackStatus = ResolvePreviousStatus(quotation) ?? DefaultQuotationStatus;
            ApplyStatusAudit(quotation, fallbackStatus, operatorLabel, now);
        }
        else if (IsCancellationStatus(quotation.Status))
        {
            // 已取消但原因被移除時，回退到可用的上一個狀態，避免估價單停留在取消狀態。
            var fallbackStatus = ResolvePreviousStatus(quotation) ?? DefaultQuotationStatus;
            ClearCancellationAudit(quotation);
            ApplyStatusAudit(quotation, fallbackStatus, operatorLabel, now);
        }
        else
        {
            // 一般編輯仍需更新最後異動紀錄，保持狀態時間一致。
            quotation.ModificationTimestamp = now;
            quotation.ModifiedBy = operatorLabel;
            quotation.CurrentStatusDate = now;
            quotation.CurrentStatusUser = operatorLabel;
        }

        quotation.Reject = rejectFlag ? true : null;
        quotation.RejectReason = rejectFlag ? unrepairableReason : null;

        var panelBeatFlag = !string.IsNullOrEmpty(suggestedPaintReason);
        quotation.PanelBeat = panelBeatFlag ? "1" : null;
        quotation.PanelBeatReason = panelBeatFlag ? suggestedPaintReason : null;

        quotation.Valuation = valuation;

        var extraData = BuildExtraData(
            effectiveDamages,
            carBodyConfirmation,
            otherFee,
            roundingDiscount,
            percentageDiscount,
            discountReason,
            estimatedRepairDays,
            estimatedRepairHours,
            estimatedRestorationPercentage,
            suggestedPaintReason,
            unrepairableReason,
            categoryAdjustmentsForExtra);
        quotation.Remark = SerializeRemark(maintenanceRemark, extraData);

        await _context.SaveChangesAsync(cancellationToken);

        if (photoUids.Count > 0)
        {
            await _photoService.BindToQuotationAsync(quotation.QuotationUid, photoUids, cancellationToken);
        }

        await SyncDamagePhotoMetadataAsync(effectiveDamages, carBodyConfirmation?.SignaturePhotoUid, cancellationToken);
        await MarkSignaturePhotoAsync(carBodyConfirmation?.SignaturePhotoUid, cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 更新估價單 {QuotationUid} 完成。", operatorLabel, quotation.QuotationUid);
    }

    /// <inheritdoc />
    public async Task<QuotationStatusChangeResponse> CompleteEvaluationAsync(QuotationEvaluateRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價完成的參數。");
        }

        // 估價單編號為必要欄位，缺少時直接終止流程。
        var quotationNo = EnsureRequestHasQuotationNo(request);

        cancellationToken.ThrowIfCancellationRequested();

        // 透過估價單編號尋找可更新的實體。
        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無需標記估價完成的估價單。");
        }

        // 僅允許從 110(估價中) 或已是 180 狀態再標記為估價完成，避免破壞狀態流程。
        var currentStatus = NormalizeOptionalText(quotation.Status);

        if (currentStatus is not null && currentStatus != "110" && currentStatus != "180")
        {
            var statusLabel = string.IsNullOrWhiteSpace(quotation.Status) ? "未知" : quotation.Status;
            throw new QuotationManagementException(HttpStatusCode.Conflict, $"估價單目前狀態為 {statusLabel}，無法標記為估價完成。");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        // 統一透過狀態稽核方法寫入狀態與操作時間，確保歷史記錄完整。
        ApplyStatusAudit(quotation, "180", operatorLabel, now);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將估價單 {QuotationNo} 標記為估價完成。", operatorLabel, quotation.QuotationNo);

        return BuildStatusChangeResponse(quotation, now);
    }

    /// <inheritdoc />
    public async Task<QuotationStatusChangeResponse> CancelQuotationAsync(QuotationCancelRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供取消估價單的參數。");
        }

        return await CancelQuotationInternalAsync(request, operatorName, cancellationToken, false, "估價單已取消");
    }

    /// <inheritdoc />
    public async Task<QuotationStatusChangeResponse> ConvertToReservationAsync(QuotationReservationRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供轉預約的參數。");
        }

        var quotationNo = EnsureRequestHasQuotationNo(request);

        var reservationDate = NormalizeOptionalDate(request.ReservationDate);
        if (!reservationDate.HasValue)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供有效的預約日期。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無要轉預約的估價單。");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        quotation.BookDate = reservationDate;
        ApplyStatusAudit(quotation, "190", operatorLabel, now);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將估價單 {QuotationNo} 轉為預約狀態。", operatorLabel, quotation.QuotationNo);

        return BuildStatusChangeResponse(quotation, now);
    }

    /// <inheritdoc />
    public async Task<QuotationStatusChangeResponse> UpdateReservationDateAsync(QuotationReservationRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供更改預約的參數。");
        }

        var quotationNo = EnsureRequestHasQuotationNo(request);

        var reservationDate = NormalizeOptionalDate(request.ReservationDate);
        if (!reservationDate.HasValue)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供有效的預約日期。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無要更新預約日期的估價單。");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        quotation.BookDate = reservationDate;
        ApplyStatusAudit(quotation, "190", operatorLabel, now);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 調整估價單 {QuotationNo} 的預約日期。", operatorLabel, quotation.QuotationNo);

        return BuildStatusChangeResponse(quotation, now);
    }

    /// <inheritdoc />
    public async Task<QuotationStatusChangeResponse> CancelReservationAsync(QuotationCancelRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供取消預約的參數。");
        }

        return await CancelQuotationInternalAsync(request, operatorName, cancellationToken, true, "預約已取消");
    }

    /// <inheritdoc />
    public async Task<QuotationStatusChangeResponse> RevertQuotationStatusAsync(QuotationRevertStatusRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供狀態回溯的參數。");
        }

        var quotationNo = EnsureRequestHasQuotationNo(request);

        cancellationToken.ThrowIfCancellationRequested();

        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無需回溯狀態的估價單。");
        }

        var previousStatus = ResolvePreviousStatus(quotation);
        if (previousStatus is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "估價單缺少可回溯的上一個狀態。");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();

        if (IsCancellationStatus(quotation.Status))
        {
            ClearCancellationAudit(quotation);
        }

        ApplyStatusAudit(quotation, previousStatus, operatorLabel, now);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將估價單 {QuotationNo} 狀態回溯至 {Status}。", operatorLabel, quotation.QuotationNo, previousStatus);

        return BuildStatusChangeResponse(quotation, now);
    }

    /// <inheritdoc />
    public async Task<QuotationMaintenanceConversionResponse> ConvertToMaintenanceAsync(QuotationMaintenanceRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供轉維修的參數。");
        }

        var quotationNo = EnsureRequestHasQuotationNo(request);

        cancellationToken.ThrowIfCancellationRequested();

        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無要轉維修的估價單。");
        }

        var existingOrder = await _context.Orders
            .AsNoTracking()
            .Where(order => order.QuatationUid == quotation.QuotationUid)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingOrder is not null)
        {
            var orderNo = existingOrder.OrderNo ?? existingOrder.OrderUid;
            throw new QuotationManagementException(HttpStatusCode.Conflict, $"估價單已建立維修單，編號：{orderNo}");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();
        var orderSerial = await GenerateNextOrderSerialAsync(now, cancellationToken);
        var orderUid = BuildOrderUid();
        var orderNoNew = BuildOrderNo(orderSerial, now);
        var (plainRemark, _) = ParseRemark(quotation.Remark);
        var amount = CalculateOrderAmount(quotation.Valuation, quotation.Discount);
        var orderEstimationUid = NormalizeOptionalText(quotation.EstimationTechnicianUid)
            ?? NormalizeOptionalText(quotation.UserUid);

        // ---------- 製單技師預設回落至估價技師，確保舊資料亦能帶出製單資訊 ----------
        var orderCreatorUid = NormalizeOptionalText(quotation.CreatorTechnicianUid)
            ?? orderEstimationUid;

        var order = new Order
        {
            OrderUid = orderUid,
            OrderNo = orderNoNew,
            SerialNum = orderSerial,
            CreationTimestamp = now,
            CreatedBy = operatorLabel,
            ModificationTimestamp = now,
            ModifiedBy = operatorLabel,
            // ---------- 儲存估價與使用者資料，方便維修單查詢顯示 ----------
            UserUid = NormalizeOptionalText(quotation.UserUid) ?? orderEstimationUid ?? operatorLabel,
            UserName = NormalizeOptionalText(quotation.UserName) ?? operatorLabel,
            EstimationTechnicianUid = orderEstimationUid,
            CreatorTechnicianUid = orderCreatorUid,
            StoreUid = quotation.StoreUid,
            Date = DateOnly.FromDateTime(now),
            Status = "210",
            Status210Date = now,
            Status210User = operatorLabel,
            CurrentStatusDate = now,
            CurrentStatusUser = operatorLabel,
            QuatationUid = quotation.QuotationUid,
            CarUid = quotation.CarUid,
            CarNoInputGlobal = quotation.CarNoInputGlobal,
            CarNoInput = quotation.CarNoInput,
            CarNo = quotation.CarNo,
            Brand = quotation.Brand,
            Model = quotation.Model,
            Color = quotation.Color,
            CarRemark = quotation.CarRemark,
            Milage = quotation.Milage,
            BrandModel = quotation.BrandModel,
            CustomerUid = quotation.CustomerUid,
            CustomerType = quotation.CustomerType,
            PhoneInputGlobal = quotation.PhoneInputGlobal,
            PhoneInput = quotation.PhoneInput,
            Phone = quotation.Phone,
            Name = quotation.Name,
            Gender = quotation.Gender,
            Connect = quotation.Connect,
            County = quotation.County,
            Township = quotation.Township,
            Source = quotation.Source,
            Reason = quotation.Reason,
            Email = quotation.Email,
            ConnectRemark = quotation.ConnectRemark,
            BookDate = quotation.BookDate?.ToString("yyyy-MM-dd"),
            BookMethod = quotation.BookMethod,
            WorkDate = quotation.FixDate?.ToString("yyyy-MM-dd"),
            FixType = quotation.FixType,
            CarReserved = quotation.CarReserved,
            Content = plainRemark,
            Remark = quotation.Remark,
            Valuation = quotation.Valuation,
            DiscountPercent = quotation.DiscountPercent,
            Discount = quotation.Discount,
            DiscountReason = quotation.DiscountReason,
            Amount = amount,
            FlagRegularCustomer = quotation.FlagRegularCustomer,
            FlagExternalCooperation = false
        };

        // ---------- 類別折扣欄位同步 ----------
        ApplyCategoryAdjustments(order, ExtractCategoryAdjustments(quotation));

        _context.Orders.Add(order);

        var relatedPhotos = await _context.PhotoData
            .Where(photo => photo.QuotationUid == quotation.QuotationUid)
            .ToListAsync(cancellationToken);

        foreach (var photo in relatedPhotos)
        {
            photo.RelatedUid = orderUid;
        }

        ApplyStatusAudit(quotation, "191", operatorLabel, now);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 將估價單 {QuotationNo} 轉為維修單 {OrderNo}。", operatorLabel, quotation.QuotationNo, orderNoNew);

        return BuildMaintenanceResponse(quotation, order, now);
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 針對估價單查詢套用編號的過濾條件。
    /// </summary>
    private static IQueryable<Quatation> ApplyQuotationFilter(IQueryable<Quatation> query, string quotationNo)
    {
        if (string.IsNullOrWhiteSpace(quotationNo))
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價單編號。");
        }

        var normalizedQuotationNo = quotationNo.Trim();
        return query.Where(q => q.QuotationNo == normalizedQuotationNo);
    }

    /// <summary>
    /// 建立估價單唯一識別碼，使用 Q_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildQuotationUid()
    {
        // 以 Q_ 開頭並接續大寫 GUID，對齊前端既有格式需求，方便辨識資料來源。
        return $"Q_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 依照序號與建立時間產生估價單編號。
    /// </summary>
    private static string BuildQuotationNo(int serialNumber, DateTime timestamp)
    {
        // 採用 Q + 年份末兩碼 + 月份 + 四碼流水號（例如：Q25070078），
        // 與舊系統保持一致以便前後端串接查詢。
        return $"Q{timestamp:yyMM}{serialNumber:0000}";
    }

    /// <summary>
    /// 建立維修單唯一識別碼，使用 O_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildOrderUid()
    {
        return $"O_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 依據序號與時間產生維修單編號。
    /// </summary>
    private static string BuildOrderNo(int serialNumber, DateTime timestamp)
    {
        return $"O{timestamp:yyMM}{serialNumber:0000}";
    }

    /// <summary>
    /// 根據品牌與車型組出顯示文字。
    /// </summary>
    private static string? BuildBrandModel(string? brand, string? model)
    {
        if (string.IsNullOrWhiteSpace(brand) && string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(brand))
        {
            return model;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return brand;
        }

        return $"{brand} {model}";
    }

    /// <summary>
    /// 產生下一個估價單序號，使用目前資料庫最大值加一。
    /// </summary>
    private async Task<int> GenerateNextSerialNumberAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        // 依照當前年月建立序號前綴，確保每月重新由 0001 開始遞增。
        var prefix = $"Q{timestamp:yyMM}";

        // 先以估價單編號前綴搜尋同一個年月的資料，避免跨月序號相互影響。
        var prefixCandidates = await _context.Quatations
            .AsNoTracking()
            .Where(q => !string.IsNullOrEmpty(q.QuotationNo) && EF.Functions.Like(q.QuotationNo!, prefix + "%"))
            .OrderByDescending(q => q.SerialNum)
            .ThenByDescending(q => q.QuotationNo)
            .Select(q => new SerialCandidate(q.SerialNum, q.QuotationNo))
            .Take(SerialCandidateFetchCount)
            .ToListAsync(cancellationToken);

        var maxSerial = ExtractMaxSerial(prefixCandidates, prefix);

        if (maxSerial == 0)
        {
            // 若舊資料缺少編號前綴，改用建立時間落在當月的紀錄再次比對，確保同月仍能延續序號。
            var monthStart = new DateTime(timestamp.Year, timestamp.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthCandidates = await _context.Quatations
                .AsNoTracking()
                .Where(q => q.CreationTimestamp >= monthStart && q.CreationTimestamp < monthEnd)
                .OrderByDescending(q => q.SerialNum)
                .ThenByDescending(q => q.QuotationNo)
                .Select(q => new SerialCandidate(q.SerialNum, q.QuotationNo))
                .Take(SerialCandidateFetchCount)
                .ToListAsync(cancellationToken);

            maxSerial = ExtractMaxSerial(monthCandidates, prefix);
        }

        return maxSerial + 1;
    }

    /// <summary>
    /// 產生維修單序號，沿用估價單的每月遞增規則。
    /// </summary>
    private async Task<int> GenerateNextOrderSerialAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        var prefix = $"O{timestamp:yyMM}";

        var prefixCandidates = await _context.Orders
            .AsNoTracking()
            .Where(order => !string.IsNullOrEmpty(order.OrderNo) && EF.Functions.Like(order.OrderNo!, prefix + "%"))
            .OrderByDescending(order => order.SerialNum)
            .ThenByDescending(order => order.OrderNo)
            .Select(order => new SerialCandidate(order.SerialNum, order.OrderNo))
            .Take(SerialCandidateFetchCount)
            .ToListAsync(cancellationToken);

        var maxSerial = ExtractMaxSerial(prefixCandidates, prefix);

        if (maxSerial == 0)
        {
            var monthStart = new DateTime(timestamp.Year, timestamp.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthCandidates = await _context.Orders
                .AsNoTracking()
                .Where(order => order.CreationTimestamp >= monthStart && order.CreationTimestamp < monthEnd)
                .OrderByDescending(order => order.SerialNum)
                .ThenByDescending(order => order.OrderNo)
                .Select(order => new SerialCandidate(order.SerialNum, order.OrderNo))
                .Take(SerialCandidateFetchCount)
                .ToListAsync(cancellationToken);

            maxSerial = ExtractMaxSerial(monthCandidates, prefix);
        }

        return maxSerial + 1;
    }

    /// <summary>
    /// 估價單序號候選資料結構，封裝序號欄位與估價單編號，便於後續解析。
    /// </summary>
    private sealed record SerialCandidate(int? SerialNum, string? DocumentNo);

    /// <summary>
    /// 從資料庫撈取的候選資料中取出最大序號，支援從 QuotationNo 解析舊資料的流水號。
    /// </summary>
    private static int ExtractMaxSerial(IEnumerable<SerialCandidate> candidates, string prefix)
    {
        var maxSerial = 0;

        foreach (var candidate in candidates)
        {
            // 先比對 SerialNum 欄位，若資料表已填寫則直接採用。
            if (candidate.SerialNum is int serial && serial > maxSerial)
            {
                maxSerial = serial;
            }

            // 再從編號欄位補捉舊資料留下的序號數字，避免序號回到 0001。
            if (candidate.DocumentNo is string documentNo)
            {
                var parsedSerial = TryParseSerialFromDocumentNo(documentNo, prefix);
                if (parsedSerial.HasValue && parsedSerial.Value > maxSerial)
                {
                    maxSerial = parsedSerial.Value;
                }
            }
        }

        return maxSerial;
    }

    /// <summary>
    /// 嘗試從估價單編號中解析四碼流水號，支援舊格式保留的連字號或其他符號。
    /// </summary>
    private static int? TryParseSerialFromDocumentNo(string? documentNo, string prefix)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
        {
            return null;
        }

        var trimmed = documentNo.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = trimmed[prefix.Length..];
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return null;
        }

        var digits = new string(suffix.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }

        return int.TryParse(digits, out var serialNumber) ? serialNumber : null;
    }

    /// <summary>
    /// 將修復完成度百分比轉為資料表 FixExpect 欄位使用的字串格式。
    /// </summary>
    private static string? FormatEstimatedRestorationPercentage(decimal? percentage)
    {
        if (!percentage.HasValue)
        {
            return null;
        }

        // 固定使用不帶百分號的字串並採用 InvariantCulture，避免不同語系造成小數點格式差異。
        return percentage.Value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 將 FixExpect 字串解析為修復完成度百分比數值，支援舊資料保留的百分號格式。
    /// </summary>
    private static decimal? ParseEstimatedRestorationPercentage(string? fixExpect)
    {
        if (string.IsNullOrWhiteSpace(fixExpect))
        {
            return null;
        }

        var trimmed = fixExpect.Trim();

        if (trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        // 先以 InvariantCulture 嘗試解析，確保與格式化時行為一致。
        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        // 若資料源於舊系統語系設定，也允許使用目前文化格式解析。
        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentValue))
        {
            return currentValue;
        }

        return null;
    }

    /// <summary>
    /// 依據登入門市或技師所屬門市載入門市主檔資料。
    /// </summary>
    private async Task<StoreEntity?> GetStoreEntityAsync(string? storeUid, TechnicianEntity? technician, CancellationToken cancellationToken)
    {
        var normalizedStoreUid = NormalizeOptionalText(storeUid);
        if (normalizedStoreUid is not null)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.StoreUid == normalizedStoreUid, cancellationToken);

            if (store is null)
            {
                throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的門市資料，請重新選擇門市。");
            }

            return store;
        }

        return await GetStoreEntityAsync(technician, cancellationToken);
    }

    /// <summary>
    /// 依據技師識別碼載入技師與所屬門市資料，若未提供識別碼則回傳 null。
    /// </summary>
    private async Task<TechnicianEntity?> GetTechnicianEntityAsync(string? technicianUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(technicianUid);
        if (normalizedUid is null)
        {
            return null;
        }

        var technician = await _context.Technicians
            .AsNoTracking()
            .Include(entity => entity.Store)
            .FirstOrDefaultAsync(entity => entity.TechnicianUid == normalizedUid, cancellationToken);

        if (technician is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的技師資料，請重新選擇技師。");
        }

        return technician;
    }

    /// <summary>
    /// 根據技師所屬門市取得門市主檔資料，提供登入門市缺漏時的備援。
    /// </summary>
    private async Task<StoreEntity?> GetStoreEntityAsync(TechnicianEntity? technician, CancellationToken cancellationToken)
    {
        if (technician is null)
        {
            return null;
        }

        if (technician.Store is not null)
        {
            return technician.Store;
        }

        var normalizedStoreUid = NormalizeOptionalText(technician.StoreUid);
        if (normalizedStoreUid is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的門市資料。");
        }

        var store = await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.StoreUid == normalizedStoreUid, cancellationToken);

        if (store is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的門市資料。");
        }

        return store;
    }

    /// <summary>
    /// 依據車輛識別碼取得車輛主檔資料，若未提供識別碼則回傳 null。
    /// </summary>
    private async Task<CarEntity?> GetCarEntityAsync(string? carUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(carUid);
        if (normalizedUid is null)
        {
            return null;
        }

        var car = await _context.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.CarUid == normalizedUid, cancellationToken);

        if (car is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的車輛資料，請重新選擇車輛。");
        }

        return car;
    }

    /// <summary>
    /// 依據客戶識別碼取得客戶主檔資料，若未提供識別碼則回傳 null。
    /// </summary>
    private async Task<CustomerEntity?> GetCustomerEntityAsync(string? customerUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(customerUid);
        if (normalizedUid is null)
        {
            return null;
        }

        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.CustomerUid == normalizedUid, cancellationToken);

        if (customer is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的客戶資料，請重新選擇客戶。");
        }

        return customer;
    }

    /// <summary>
    /// 嘗試尋找可供更新的估價單資料。
    /// </summary>
    private async Task<Quatation?> FindQuotationForUpdateAsync(string quotationNo, CancellationToken cancellationToken)
    {
        var query = ApplyQuotationFilter(_context.Quatations, quotationNo);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// 統一處理取消估價與取消預約的流程。
    /// </summary>
    private async Task<QuotationStatusChangeResponse> CancelQuotationInternalAsync(
        QuotationCancelRequest request,
        string operatorName,
        CancellationToken cancellationToken,
        bool defaultClearReservation,
        string defaultReason)
    {
        var quotationNo = EnsureRequestHasQuotationNo(request);

        cancellationToken.ThrowIfCancellationRequested();

        var quotation = await FindQuotationForUpdateAsync(quotationNo, cancellationToken);
        if (quotation is null)
        {
            var message = defaultClearReservation ? "查無需要取消預約的估價單。" : "查無需要取消的估價單。";
            throw new QuotationManagementException(HttpStatusCode.NotFound, message);
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var now = GetTaipeiNow();
        var effectiveReason = NormalizeOptionalText(request.Reason) ?? defaultReason;

        quotation.RejectReason = effectiveReason;
        quotation.Reject = !string.IsNullOrWhiteSpace(effectiveReason);

        var shouldClearReservation = defaultClearReservation || request.ClearReservation;
        if (shouldClearReservation)
        {
            quotation.BookDate = null;
            quotation.BookMethod = null;
        }

        ApplyStatusAudit(quotation, CancellationStatus, operatorLabel, now);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 取消估價單 {QuotationNo}，原因：{Reason}。",
            operatorLabel,
            quotation.QuotationNo,
            effectiveReason);

        return BuildStatusChangeResponse(quotation, now);
    }

    /// <summary>
    /// 驗證請求是否具備估價單編號，若缺少則轉為服務例外。
    /// </summary>
    private static string EnsureRequestHasQuotationNo(QuotationActionRequestBase request)
    {
        try
        {
            return request.EnsureAndGetQuotationNo();
        }
        catch (ValidationException ex)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <summary>
    /// 判斷目前狀態是否為取消 (195)。
    /// </summary>
    private static bool IsCancellationStatus(string? status)
    {
        var normalized = NormalizeOptionalText(status);
        return string.Equals(normalized, CancellationStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判斷目前狀態是否為不能維修 (115)。
    /// </summary>
    private static bool IsUnrepairableStatus(string? status)
    {
        var normalized = NormalizeOptionalText(status);
        return string.Equals(normalized, UnrepairableStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 回溯或轉換狀態時清除取消紀錄，避免保留 199 欄位資訊。
    /// </summary>
    private static void ClearCancellationAudit(Quatation quotation)
    {
        quotation.Reject = false;
        quotation.RejectReason = null;
        quotation.Status199Timestamp = null;
        quotation.Status199User = null;
    }

    /// <summary>
    /// 將狀態異動統一記錄於估價單欄位與狀態時間欄位。
    /// </summary>
    private static void ApplyStatusAudit(Quatation quotation, string statusCode, string operatorLabel, DateTime timestamp)
    {
        quotation.Status = statusCode;
        quotation.ModificationTimestamp = timestamp;
        quotation.ModifiedBy = operatorLabel;
        quotation.CurrentStatusDate = timestamp;
        quotation.CurrentStatusUser = operatorLabel;

        switch (statusCode)
        {
            case DefaultQuotationStatus:
                quotation.Status110Timestamp = timestamp;
                quotation.Status110User = operatorLabel;
                break;
            case "180":
                quotation.Status180Timestamp = timestamp;
                quotation.Status180User = operatorLabel;
                break;
            case "190":
                quotation.Status190Timestamp = timestamp;
                quotation.Status190User = operatorLabel;
                break;
            case "191":
                quotation.Status191Timestamp = timestamp;
                quotation.Status191User = operatorLabel;
                break;
            case CancellationStatus:
                // 資料表目前僅提供 Status199 欄位，暫用來記錄 195 取消狀態的操作時間。
                quotation.Status199Timestamp = timestamp;
                quotation.Status199User = operatorLabel;
                break;
        }
    }

    /// <summary>
    /// 由歷史狀態時間判斷上一個狀態碼。
    /// </summary>
    private static string? ResolvePreviousStatus(Quatation quotation)
    {
        var currentStatus = NormalizeOptionalText(quotation.Status);

        if (string.Equals(currentStatus, "186", StringComparison.OrdinalIgnoreCase))
        {
            return "110";
        }

        if (string.Equals(currentStatus, "196", StringComparison.OrdinalIgnoreCase))
        {
            return "190";
        }

        // 依照流程定義建立狀態堆疊，確保回朔會依序往前尋找。
        // 狀態 195 的時間點仍儲存在 Status199 欄位，因此在此明確對應。
        var statusFlow = new List<(string Code, DateTime? Timestamp)>
        {
            (DefaultQuotationStatus, quotation.Status110Timestamp),
            ("180", quotation.Status180Timestamp),
            ("190", quotation.Status190Timestamp),
            ("191", quotation.Status191Timestamp),
            (CancellationStatus, quotation.Status199Timestamp)
        };

        if (string.IsNullOrWhiteSpace(currentStatus))
        {
            // 若估價單目前缺少狀態，保守地回傳最晚的有效狀態，避免流程中斷。
            return statusFlow
                .Where(item => item.Timestamp.HasValue)
                .OrderByDescending(item => item.Timestamp!.Value)
                .Select(item => item.Code)
                .FirstOrDefault();
        }

        var currentIndex = statusFlow.FindIndex(item =>
            string.Equals(item.Code, currentStatus, StringComparison.OrdinalIgnoreCase));

        if (currentIndex < 0)
        {
            // 若當前狀態未在流程清單中（例如 115 不能維修），
            // 則回傳最後一個具備時間戳記的狀態作為回退依據。
            return statusFlow
                .Where(item => item.Timestamp.HasValue)
                .OrderByDescending(item => item.Timestamp!.Value)
                .Select(item => item.Code)
                .FirstOrDefault();
        }

        if (currentIndex == 0)
        {
            // 找不到對應狀態或已經是最初狀態（110），便無法再往前回朔。
            return null;
        }

        for (var index = currentIndex - 1; index >= 0; index--)
        {
            var (code, timestamp) = statusFlow[index];
            if (timestamp.HasValue)
            {
                // 由近而遠尋找已出現過的狀態，確保可以一路回朔至 110。
                return code;
            }
        }

        return null;
    }

    /// <summary>
    /// 建立狀態異動回應，統一處理預約日期轉換。
    /// </summary>
    private static QuotationStatusChangeResponse BuildStatusChangeResponse(Quatation quotation, DateTime statusTime)
    {
        return new QuotationStatusChangeResponse
        {
            QuotationUid = quotation.QuotationUid,
            QuotationNo = quotation.QuotationNo,
            Status = quotation.Status ?? string.Empty,
            StatusChangedAt = statusTime,
            ReservationDate = ConvertDateOnlyToDateTime(quotation.BookDate)
        };
    }

    /// <summary>
    /// 建立轉維修回應，包含新建工單資訊。
    /// </summary>
    private static QuotationMaintenanceConversionResponse BuildMaintenanceResponse(Quatation quotation, Order order, DateTime statusTime)
    {
        return new QuotationMaintenanceConversionResponse
        {
            QuotationUid = quotation.QuotationUid,
            QuotationNo = quotation.QuotationNo,
            Status = quotation.Status ?? string.Empty,
            StatusChangedAt = statusTime,
            ReservationDate = ConvertDateOnlyToDateTime(quotation.BookDate),
            OrderUid = order.OrderUid,
            OrderNo = order.OrderNo ?? string.Empty,
            OrderCreatedAt = order.CreationTimestamp ?? statusTime
        };
    }

    /// <summary>
    /// 依照估價金額與折扣計算維修單應付金額，避免負數結果。
    /// </summary>
    private static decimal? CalculateOrderAmount(decimal? valuation, decimal? discount)
    {
        if (!valuation.HasValue)
        {
            return null;
        }

        var amount = valuation.Value - (discount ?? 0m);
        return amount < 0 ? 0m : amount;
    }

    /// <summary>
    /// 將 remark 字串還原為可讀取的備註與擴充資料。
    /// </summary>
    private static (string? PlainRemark, QuotationExtraData? Extra) ParseRemark(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return (null, null);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<QuotationRemarkEnvelope>(remark, JsonOptions);
            if (envelope is null)
            {
                return (remark, null);
            }

            return (NormalizeOptionalText(envelope.PlainRemark), envelope.Extra);
        }
        catch (JsonException)
        {
            return (remark, null);
        }
    }

    /// <summary>
    /// 綜合傷痕、類別費用與折扣資訊，計算估價金額與折扣摘要。
    /// </summary>
    /// <param name="hasExplicitAdjustments">
    /// 指出是否為新版類別格式的資料來源，避免舊資料被誤判為新格式。
    /// </param>
    private static MaintenanceFinancialSummary CalculateMaintenanceFinancialSummary(
        List<QuotationDamageItem> damages,
        decimal? otherFee,
        decimal? roundingDiscount,
        decimal? percentageDiscount,
        string? discountReason,
        QuotationMaintenanceCategoryAdjustmentCollection? categoryAdjustments,
        bool hasExplicitAdjustments,
        string? fallbackCategoryKey,
        bool preferBeautyAlias)
    {
        var normalizedReason = NormalizeOptionalText(discountReason);
        var normalization = NormalizeMaintenanceAdjustments(
            categoryAdjustments,
            otherFee,
            percentageDiscount,
            normalizedReason,
            fallbackCategoryKey,
            preferBeautyAlias);
        var adjustments = normalization.Adjustments;
        // 僅在外部明確標註有帶入類別資料時，才視為使用新版格式。
        var explicitAdjustments = hasExplicitAdjustments && normalization.HasExplicitAdjustments;
        var categoryTotals = CalculateCategoryDamageTotals(damages);

        decimal aggregatedOtherFeeValue = 0m;
        var hasOtherFee = false;
        var hasBaseAmount = false;
        decimal totalBase = 0m;
        decimal totalDiscountAmount = 0m;

        foreach (var (key, adjustment) in EnumerateAdjustmentPairs(adjustments))
        {
            var damageSubtotal = categoryTotals.TryGetValue(key, out var subtotal) ? subtotal : 0m;
            var categoryOtherFee = adjustment.OtherFee ?? 0m;
            var hasOtherFeeValue = adjustment.OtherFee.HasValue;

            if (damageSubtotal > 0m || hasOtherFeeValue)
            {
                hasBaseAmount = true;
            }

            if (hasOtherFeeValue)
            {
                aggregatedOtherFeeValue += adjustment.OtherFee.Value;
                hasOtherFee = true;
            }

            var categoryBase = damageSubtotal + categoryOtherFee;
            totalBase += categoryBase;

            if (adjustment.PercentageDiscount.HasValue && adjustment.PercentageDiscount.Value != 0m)
            {
                var discountRate = adjustment.PercentageDiscount.Value / 100m;
                totalDiscountAmount += categoryBase * discountRate;
            }
        }

        var aggregatedOtherFee = hasOtherFee ? aggregatedOtherFeeValue : otherFee;
        var discountReasonSummary = BuildAggregatedDiscountReason(adjustments, normalizedReason);

        decimal? effectivePercentage = null;
        if (totalDiscountAmount != 0m && totalBase != 0m)
        {
            effectivePercentage = totalDiscountAmount / totalBase * 100m;
        }
        else
        {
            effectivePercentage = ExtractFirstPercentageDiscount(adjustments) ?? percentageDiscount;
        }

        var netAmount = totalBase - totalDiscountAmount;
        var hasDiscount = totalDiscountAmount != 0m;

        if (roundingDiscount.HasValue)
        {
            netAmount -= roundingDiscount.Value;
            if (roundingDiscount.Value != 0m)
            {
                hasDiscount = true;
            }
        }

        decimal? valuation = netAmount;
        if (!hasBaseAmount && !hasDiscount)
        {
            valuation = null;
        }
        else if (valuation < 0m)
        {
            valuation = 0m;
        }

        return new MaintenanceFinancialSummary
        {
            Adjustments = adjustments,
            HasAdjustmentData = HasCategoryAdjustments(adjustments),
            HasExplicitAdjustments = explicitAdjustments,
            OtherFee = aggregatedOtherFee,
            EffectivePercentageDiscount = effectivePercentage,
            DiscountReason = discountReasonSummary,
            Valuation = valuation
        };
    }

    /// <summary>
    /// 將 remark 內容轉換為儲存格式，必要時包裝擴充資料。
    /// </summary>
    private static string SerializeRemark(string? plainRemark, QuotationExtraData? extraData)
    {
        var normalizedRemark = NormalizeOptionalText(plainRemark);
        if (extraData is null)
        {
            return normalizedRemark ?? string.Empty;
        }

        var envelope = new QuotationRemarkEnvelope
        {
            Version = 2,
            PlainRemark = normalizedRemark,
            Extra = extraData
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    /// <summary>
    /// 建立 remark 擴充資料，集中儲存傷痕、簽名與折扣資訊。
    /// </summary>
    private static QuotationExtraData? BuildExtraData(
        List<QuotationDamageItem> damages,
        QuotationCarBodyConfirmation? carBody,
        decimal? otherFee,
        decimal? roundingDiscount,
        decimal? percentageDiscount,
        string? discountReason,
        int? estimatedRepairDays,
        int? estimatedRepairHours,
        decimal? estimatedRestorationPercentage,
        string? suggestedPaintReason,
        string? unrepairableReason,
        QuotationMaintenanceCategoryAdjustmentCollection? categoryAdjustments)
    {
        var hasDamages = damages is { Count: > 0 };
        var hasCarBody = HasCarBodyContent(carBody);
        var hasMaintenanceExtra = otherFee.HasValue
            || roundingDiscount.HasValue
            || percentageDiscount.HasValue
            || !string.IsNullOrWhiteSpace(discountReason)
            || estimatedRepairDays.HasValue
            || estimatedRepairHours.HasValue
            || estimatedRestorationPercentage.HasValue
            || !string.IsNullOrWhiteSpace(suggestedPaintReason)
            || !string.IsNullOrWhiteSpace(unrepairableReason)
            || HasCategoryAdjustments(categoryAdjustments);

        if (!hasDamages && !hasCarBody && !hasMaintenanceExtra)
        {
            return null;
        }

        return new QuotationExtraData
        {
            Damages = hasDamages ? damages : null,
            CarBodyConfirmation = hasCarBody ? carBody : null,
            OtherFee = otherFee,
            RoundingDiscount = roundingDiscount,
            PercentageDiscount = percentageDiscount,
            DiscountReason = discountReason,
            EstimatedRepairDays = estimatedRepairDays,
            EstimatedRepairHours = estimatedRepairHours,
            EstimatedRestorationPercentage = estimatedRestorationPercentage,
            SuggestedPaintReason = suggestedPaintReason,
            UnrepairableReason = unrepairableReason,
            CategoryAdjustments = HasCategoryAdjustments(categoryAdjustments)
                ? CloneCategoryAdjustments(categoryAdjustments)
                : null
        };
    }

    /// <summary>
    /// 正規化維修類別的折扣設定，必要時補入舊欄位資料作為預設值。
    /// </summary>
    private static MaintenanceAdjustmentNormalizationResult NormalizeMaintenanceAdjustments(
        QuotationMaintenanceCategoryAdjustmentCollection? source,
        decimal? fallbackOtherFee,
        decimal? fallbackPercentageDiscount,
        string? fallbackDiscountReason,
        string? fallbackCategoryKey,
        bool preferBeautyAlias)
    {
        var adjustments = CloneCategoryAdjustments(source);
        var hasExplicitAdjustments = HasCategoryAdjustments(source);

        var normalizedFallbackKey = preferBeautyAlias ? "other" : fallbackCategoryKey;
        var targetCategory = EnsureCategoryAdjustment(adjustments, normalizedFallbackKey);

        if (!HasOtherFee(adjustments) && fallbackOtherFee.HasValue)
        {
            // 舊資料缺少類別結構時，依據維修類型推回指定類別欄位。
            targetCategory.OtherFee = fallbackOtherFee;
        }

        if (!HasPercentageDiscount(adjustments) && fallbackPercentageDiscount.HasValue)
        {
            // 同步折扣趴數至對應類別，讓舊資料也能顯示在新畫面。
            targetCategory.PercentageDiscount = fallbackPercentageDiscount;
        }

        if (!HasDiscountReason(adjustments) && !string.IsNullOrWhiteSpace(fallbackDiscountReason))
        {
            // 補齊折扣原因，避免舊資料顯示於錯誤區塊。
            targetCategory.DiscountReason = fallbackDiscountReason;
        }

        if (!hasExplicitAdjustments && preferBeautyAlias && HasCategoryAdjustmentValue(adjustments.Other))
        {
            // 舊資料若判定為美容類別，額外複製至 beauty 欄位供前端顯示。
            adjustments.Beauty = CloneCategoryAdjustment(adjustments.Other);
        }
        else if (!HasCategoryAdjustmentValue(adjustments.Beauty))
        {
            // 保留空白美容欄位，確保回傳結構固定包含 beauty 鍵值。
            adjustments.Beauty ??= new QuotationMaintenanceCategoryAdjustment();
        }

        return new MaintenanceAdjustmentNormalizationResult
        {
            Adjustments = adjustments,
            HasExplicitAdjustments = hasExplicitAdjustments
        };
    }

    /// <summary>
    /// 建立維修類別與其設定的列舉序列，方便後續統一運算。
    /// </summary>
    private static IEnumerable<(string Key, QuotationMaintenanceCategoryAdjustment Adjustment)> EnumerateAdjustmentPairs(
        QuotationMaintenanceCategoryAdjustmentCollection adjustments)
    {
        yield return ("dent", adjustments.Dent ?? new QuotationMaintenanceCategoryAdjustment());
        yield return ("paint", adjustments.Paint ?? new QuotationMaintenanceCategoryAdjustment());
        yield return ("other", adjustments.Other ?? new QuotationMaintenanceCategoryAdjustment());
    }

    /// <summary>
    /// 依維修類別計算傷痕金額小計，供折扣運算使用。
    /// </summary>
    private static Dictionary<string, decimal> CalculateCategoryDamageTotals(IEnumerable<QuotationDamageItem> damages)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["dent"] = 0m,
            ["paint"] = 0m,
            ["other"] = 0m
        };

        if (damages is null)
        {
            return totals;
        }

        foreach (var damage in damages)
        {
            if (damage?.DisplayEstimatedAmount is not { } amount)
            {
                continue;
            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
            var groupKey = QuotationDamageFixTypeHelper.DetermineGroupKey(damage.FixType);
            var normalizedKey = NormalizeCategoryKey(groupKey) ?? "other";

            if (!totals.ContainsKey(normalizedKey))
            {
                totals[normalizedKey] = 0m;
            }

            totals[normalizedKey] += amount;
        }

        return totals;
    }

    /// <summary>
    /// 組裝折扣原因的顯示文字，若多於一個原因會加上類別標籤。
    /// </summary>
    private static string? BuildAggregatedDiscountReason(
        QuotationMaintenanceCategoryAdjustmentCollection adjustments,
        string? fallbackReason)
    {
        var entries = new List<(string Label, string? Reason)>
        {
            ("凹痕", NormalizeOptionalText(adjustments.Dent?.DiscountReason)),
            ("板烤", NormalizeOptionalText(adjustments.Paint?.DiscountReason)),
            ("其他", NormalizeOptionalText(adjustments.Other?.DiscountReason))
        };

        var nonEmpty = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Reason))
            .ToList();

        if (nonEmpty.Count == 0)
        {
            return fallbackReason;
        }

        if (nonEmpty.Count == 1)
        {
            return nonEmpty[0].Reason;
        }

        return string.Join("；", nonEmpty.Select(entry => $"{entry.Label}：{entry.Reason}"));
    }

    /// <summary>
    /// 從類別折扣設定中擷取首個有效折扣百分比，供回寫舊欄位使用。
    /// </summary>
    private static decimal? ExtractFirstPercentageDiscount(QuotationMaintenanceCategoryAdjustmentCollection adjustments)
    {
        if (adjustments.Dent?.PercentageDiscount.HasValue == true)
        {
            return adjustments.Dent.PercentageDiscount;
        }

        if (adjustments.Paint?.PercentageDiscount.HasValue == true)
        {
            return adjustments.Paint.PercentageDiscount;
        }

        if (adjustments.Other?.PercentageDiscount.HasValue == true)
        {
            return adjustments.Other.PercentageDiscount;
        }

        return null;
    }

    /// <summary>
    /// 判斷類別集合是否含有任何折扣或費用設定。
    /// </summary>
    private static bool HasCategoryAdjustments(QuotationMaintenanceCategoryAdjustmentCollection? collection)
    {
        if (collection is null)
        {
            return false;
        }

        return HasCategoryAdjustmentValue(collection.Dent)
            || HasCategoryAdjustmentValue(collection.Paint)
            || HasCategoryAdjustmentValue(collection.Other)
            || HasCategoryAdjustmentValue(collection.Beauty);
    }

    /// <summary>
    /// 判斷單一類別是否有設定費用或折扣資料。
    /// </summary>
    private static bool HasCategoryAdjustmentValue(QuotationMaintenanceCategoryAdjustment? adjustment)
    {
        if (adjustment is null)
        {
            return false;
        }

        return adjustment.OtherFee.HasValue
            || adjustment.PercentageDiscount.HasValue
            || !string.IsNullOrWhiteSpace(adjustment.DiscountReason);
    }

    /// <summary>
    /// 判斷三大類別中是否有設定額外費用。
    /// </summary>
    private static bool HasOtherFee(QuotationMaintenanceCategoryAdjustmentCollection adjustments)
    {
        return adjustments.Dent?.OtherFee.HasValue == true
            || adjustments.Paint?.OtherFee.HasValue == true
            || adjustments.Other?.OtherFee.HasValue == true
            || adjustments.Beauty?.OtherFee.HasValue == true;
    }

    /// <summary>
    /// 判斷三大類別中是否有設定折扣趴數。
    /// </summary>
    private static bool HasPercentageDiscount(QuotationMaintenanceCategoryAdjustmentCollection adjustments)
    {
        return adjustments.Dent?.PercentageDiscount.HasValue == true
            || adjustments.Paint?.PercentageDiscount.HasValue == true
            || adjustments.Other?.PercentageDiscount.HasValue == true
            || adjustments.Beauty?.PercentageDiscount.HasValue == true;
    }

    /// <summary>
    /// 判斷三大類別中是否有填寫折扣原因。
    /// </summary>
    private static bool HasDiscountReason(QuotationMaintenanceCategoryAdjustmentCollection adjustments)
    {
        return !string.IsNullOrWhiteSpace(adjustments.Dent?.DiscountReason)
            || !string.IsNullOrWhiteSpace(adjustments.Paint?.DiscountReason)
            || !string.IsNullOrWhiteSpace(adjustments.Other?.DiscountReason)
            || !string.IsNullOrWhiteSpace(adjustments.Beauty?.DiscountReason);
    }

    /// <summary>
    /// 深度複製整體類別折扣集合，避免後續修改互相影響。
    /// </summary>
    private static QuotationMaintenanceCategoryAdjustmentCollection CloneCategoryAdjustments(QuotationMaintenanceCategoryAdjustmentCollection? source)
    {
        var clone = new QuotationMaintenanceCategoryAdjustmentCollection
        {
            Dent = CloneCategoryAdjustment(source?.Dent),
            Paint = CloneCategoryAdjustment(source?.Paint),
            Beauty = CloneCategoryAdjustment(source?.Beauty),
            Other = CloneCategoryAdjustment(source?.Other)
        };

        if (!HasCategoryAdjustmentValue(clone.Other) && HasCategoryAdjustmentValue(clone.Beauty))
        {
            // 若僅提供美容欄位則同步回退至其他類別，維持原本資料庫結構。
            clone.Other = CloneCategoryAdjustment(clone.Beauty);
        }

        return clone;
    }

    /// <summary>
    /// 深度複製單一類別的折扣設定並同步正規化文字內容。
    /// </summary>
    private static QuotationMaintenanceCategoryAdjustment CloneCategoryAdjustment(QuotationMaintenanceCategoryAdjustment? source)
    {
        if (source is null)
        {
            return new QuotationMaintenanceCategoryAdjustment();
        }

        return new QuotationMaintenanceCategoryAdjustment
        {
            OtherFee = source.OtherFee,
            PercentageDiscount = source.PercentageDiscount,
            DiscountReason = NormalizeOptionalText(source.DiscountReason)
        };
    }

    /// <summary>
    /// 將類別折扣資料同步寫入估價單欄位，讓資料庫可直接查詢分類後的費用與折扣。
    /// </summary>
    private static void ApplyCategoryAdjustments(Quatation? entity, QuotationMaintenanceCategoryAdjustmentCollection? adjustments)
    {
        if (entity is null)
        {
            return;
        }

        entity.DentOtherFee = adjustments?.Dent?.OtherFee;
        entity.DentPercentageDiscount = adjustments?.Dent?.PercentageDiscount;
        entity.DentDiscountReason = NormalizeOptionalText(adjustments?.Dent?.DiscountReason);
        entity.PaintOtherFee = adjustments?.Paint?.OtherFee;
        entity.PaintPercentageDiscount = adjustments?.Paint?.PercentageDiscount;
        entity.PaintDiscountReason = NormalizeOptionalText(adjustments?.Paint?.DiscountReason);
        entity.OtherOtherFee = adjustments?.Other?.OtherFee;
        entity.OtherPercentageDiscount = adjustments?.Other?.PercentageDiscount;
        entity.OtherDiscountReason = NormalizeOptionalText(adjustments?.Other?.DiscountReason);
    }

    /// <summary>
    /// 將類別折扣資料同步寫入維修單欄位，維持估價與維修資料一致。
    /// </summary>
    private static void ApplyCategoryAdjustments(Order? entity, QuotationMaintenanceCategoryAdjustmentCollection? adjustments)
    {
        if (entity is null)
        {
            return;
        }

        entity.DentOtherFee = adjustments?.Dent?.OtherFee;
        entity.DentPercentageDiscount = adjustments?.Dent?.PercentageDiscount;
        entity.DentDiscountReason = NormalizeOptionalText(adjustments?.Dent?.DiscountReason);
        entity.PaintOtherFee = adjustments?.Paint?.OtherFee;
        entity.PaintPercentageDiscount = adjustments?.Paint?.PercentageDiscount;
        entity.PaintDiscountReason = NormalizeOptionalText(adjustments?.Paint?.DiscountReason);
        entity.OtherOtherFee = adjustments?.Other?.OtherFee;
        entity.OtherPercentageDiscount = adjustments?.Other?.PercentageDiscount;
        entity.OtherDiscountReason = NormalizeOptionalText(adjustments?.Other?.DiscountReason);
    }

    /// <summary>
    /// 由估價單欄位還原類別折扣設定，提供回傳詳情時的預設值。
    /// </summary>
    private static QuotationMaintenanceCategoryAdjustmentCollection? ExtractCategoryAdjustments(Quatation? entity)
    {
        if (entity is null)
        {
            return null;
        }

        var adjustments = new QuotationMaintenanceCategoryAdjustmentCollection
        {
            Dent = new QuotationMaintenanceCategoryAdjustment
            {
                OtherFee = entity.DentOtherFee,
                PercentageDiscount = entity.DentPercentageDiscount,
                DiscountReason = NormalizeOptionalText(entity.DentDiscountReason)
            },
            Paint = new QuotationMaintenanceCategoryAdjustment
            {
                OtherFee = entity.PaintOtherFee,
                PercentageDiscount = entity.PaintPercentageDiscount,
                DiscountReason = NormalizeOptionalText(entity.PaintDiscountReason)
            },
            Other = new QuotationMaintenanceCategoryAdjustment
            {
                OtherFee = entity.OtherOtherFee,
                PercentageDiscount = entity.OtherPercentageDiscount,
                DiscountReason = NormalizeOptionalText(entity.OtherDiscountReason)
            }
        };

        return HasCategoryAdjustments(adjustments) ? adjustments : null;
    }

    /// <summary>
    /// 合併 remark 與資料庫欄位提供的類別折扣資訊，優先採用 remark 中的明細。
    /// </summary>
    private static QuotationMaintenanceCategoryAdjustmentCollection? MergeCategoryAdjustments(
        QuotationMaintenanceCategoryAdjustmentCollection? primary,
        QuotationMaintenanceCategoryAdjustmentCollection? fallback)
    {
        if (primary is null && fallback is null)
        {
            return null;
        }

        if (primary is null)
        {
            return CloneCategoryAdjustments(fallback);
        }

        if (fallback is null)
        {
            return CloneCategoryAdjustments(primary);
        }

        var merged = new QuotationMaintenanceCategoryAdjustmentCollection
        {
            Dent = MergeCategoryAdjustment(primary.Dent, fallback.Dent),
            Paint = MergeCategoryAdjustment(primary.Paint, fallback.Paint),
            Beauty = MergeCategoryAdjustment(primary.Beauty, fallback.Beauty),
            Other = MergeCategoryAdjustment(primary.Other, fallback.Other)
        };

        return HasCategoryAdjustments(merged) ? merged : null;
    }

    /// <summary>
    /// 合併單一類別的折扣資訊，若主來源缺值則回退至欄位資料。
    /// </summary>
    private static QuotationMaintenanceCategoryAdjustment MergeCategoryAdjustment(
        QuotationMaintenanceCategoryAdjustment? primary,
        QuotationMaintenanceCategoryAdjustment? fallback)
    {
        var result = new QuotationMaintenanceCategoryAdjustment();

        result.OtherFee = primary?.OtherFee ?? fallback?.OtherFee;
        result.PercentageDiscount = primary?.PercentageDiscount ?? fallback?.PercentageDiscount;
        result.DiscountReason = NormalizeOptionalText(primary?.DiscountReason)
            ?? NormalizeOptionalText(fallback?.DiscountReason);

        return result;
    }

    /// <summary>
    /// 判斷車體確認單是否包含需儲存的內容。
    /// </summary>
    private static bool HasCarBodyContent(QuotationCarBodyConfirmation? carBody)
    {
        if (carBody is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(carBody.AnnotatedImage)
            || !string.IsNullOrWhiteSpace(carBody.SignaturePhotoUid)
            || !string.IsNullOrWhiteSpace(carBody.Signature)
            || (carBody.DamageMarkers is { Count: > 0 });
    }

    /// <summary>
    /// 整理需要綁定的照片識別碼，避免遺漏傷痕或簽名圖片。
    /// </summary>
    /// <summary>
    /// 建立估價單前檢查圖片是否存在且未被其他估價單佔用。
    /// </summary>
    private async Task EnsurePhotosAvailableForCreationAsync(IEnumerable<string> photoUids, CancellationToken cancellationToken)
    {
        if (photoUids is null)
        {
            return;
        }

        // ---------- 資料整理區 ----------
        // 先將所有圖片識別碼正規化並去重，避免重複查詢或出現空白值。
        var normalizedUids = photoUids
            .Select(NormalizeOptionalText)
            .Where(uid => uid is not null)
            .Select(uid => uid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedUids.Count == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料庫查詢 ----------
        // 僅撈取必要欄位以降低資料庫負擔，確認圖片是否存在與是否已綁定估價單。
        var photoRecords = await _context.PhotoData
            .AsNoTracking()
            .Where(photo => photo.PhotoUid != null && normalizedUids.Contains(photo.PhotoUid))
            .Select(photo => new { photo.PhotoUid, photo.QuotationUid })
            .ToListAsync(cancellationToken);

        var existingUids = new HashSet<string>(photoRecords.Select(photo => photo.PhotoUid), StringComparer.OrdinalIgnoreCase);
        var missingUids = normalizedUids
            .Where(uid => !existingUids.Contains(uid))
            .ToList();

        if (missingUids.Count > 0)
        {
            var missingList = string.Join(", ", missingUids);
            throw new QuotationManagementException(HttpStatusCode.BadRequest, $"找不到以下圖片識別碼：{missingList}，請確認是否已上傳。");
        }

        // 估價單建立時不允許使用已綁定其他估價單的圖片，避免資料錯置。
        var occupiedUids = photoRecords
            .Where(photo => !string.IsNullOrWhiteSpace(photo.QuotationUid))
            .Select(photo => photo.PhotoUid)
            .ToList();

        if (occupiedUids.Count > 0)
        {
            var occupiedList = string.Join(", ", occupiedUids);
            throw new QuotationManagementException(HttpStatusCode.BadRequest, $"以下圖片已綁定其他估價單：{occupiedList}，請重新選擇圖片或解除綁定。");
        }
    }

    private static List<string> CollectPhotoUids(IEnumerable<QuotationDamageItem> damages, QuotationCarBodyConfirmation? carBody)
    {
        var uniqueUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void TryAdd(HashSet<string> buffer, string? value)
        {
            var normalized = NormalizeOptionalText(value);
            if (normalized is null)
            {
                return;
            }

            buffer.Add(normalized);
        }

        if (damages is not null)
        {
            foreach (var damage in damages)
            {
                if (damage is null)
                {
                    continue;
                }

                TryAdd(uniqueUids, damage.Photo);
            }
        }

        if (carBody is not null)
        {
            // 車體確認單目前僅需簽名圖片，仍保留舊欄位避免歷史資料遺漏。
            TryAdd(uniqueUids, carBody.AnnotatedImage);
            TryAdd(uniqueUids, carBody.SignaturePhotoUid);
            TryAdd(uniqueUids, carBody.Signature);
        }

        return uniqueUids.ToList();
    }

    /// <summary>
    /// 將傷痕欄位同步至照片主檔，確保舊系統也能讀取到位置與金額資訊。
    /// </summary>
    private async Task SyncDamagePhotoMetadataAsync(IEnumerable<QuotationDamageItem> damages, string? signaturePhotoUid, CancellationToken cancellationToken)
    {
        if (damages is null)
        {
            return;
        }

        var metadata = new List<(string PhotoUid, string? Position, string? DentStatus, string? Description, decimal? Amount, string? FixTypeKey, string? FixTypeName)>();
        var normalizedSignatureUid = NormalizeOptionalText(signaturePhotoUid);

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
            var position = NormalizeOptionalText(damage.DisplayPosition);
            var dentStatus = NormalizeOptionalText(damage.DisplayDentStatus);
            var description = NormalizeOptionalText(damage.DisplayDescription);
            var amount = damage.DisplayEstimatedAmount;
            var fixTypeKey = NormalizeOptionalText(damage.DisplayFixType);
            var fixTypeName = NormalizeOptionalText(damage.FixTypeName);

            var normalizedCategory = QuotationDamageFixTypeHelper.Normalize(fixTypeKey)
                ?? QuotationDamageFixTypeHelper.Normalize(fixTypeName);
            if (normalizedCategory is null && !string.IsNullOrWhiteSpace(fixTypeKey))
            {
                normalizedCategory = QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeKey);
            }
            if (normalizedCategory is null && !string.IsNullOrWhiteSpace(fixTypeName))
            {
                normalizedCategory = QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeName);
            }

            if (string.IsNullOrWhiteSpace(fixTypeName) && normalizedCategory is not null)
            {
                fixTypeName = normalizedCategory;
            }

            var fixTypeCategory = normalizedCategory ?? fixTypeKey;
            if (string.IsNullOrWhiteSpace(fixTypeCategory))
            {
                fixTypeCategory = fixTypeName;
            }

            if (string.IsNullOrWhiteSpace(fixTypeName))
            {
                fixTypeName = QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeCategory);
            }

            foreach (var photoUid in EnumerateDamagePhotoUids(damage))
            {
                if (photoUid is null)
                {
                    continue;
                }

                if (normalizedSignatureUid is not null && string.Equals(photoUid, normalizedSignatureUid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                metadata.Add((photoUid, position, dentStatus, description, amount, fixTypeCategory, fixTypeName));
            }
        }

        if (metadata.Count == 0)
        {
            return;
        }

        var photoUids = metadata
            .Select(item => item.PhotoUid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (photoUids.Count == 0)
        {
            return;
        }

        var photos = await _context.PhotoData
            .Where(photo => photoUids.Contains(photo.PhotoUid))
            .ToListAsync(cancellationToken);

        var updated = false;

        foreach (var photo in photos)
        {
            var info = metadata.LastOrDefault(item => string.Equals(item.PhotoUid, photo.PhotoUid, StringComparison.OrdinalIgnoreCase));
            if (info.PhotoUid is null)
            {
                continue;
            }

            var comment = BuildDamageComment(info.Position, info.DentStatus, info.Description, info.Amount);

            if (photo.Posion != info.Position)
            {
                photo.Posion = info.Position;
                updated = true;
            }

            if (photo.PhotoShapeShow != info.DentStatus)
            {
                photo.PhotoShapeShow = info.DentStatus;
                updated = true;
            }

            if (photo.Comment != comment)
            {
                photo.Comment = comment;
                updated = true;
            }

            if (photo.Cost != info.Amount)
            {
                photo.Cost = info.Amount;
                updated = true;
            }

            var normalizedFixType = QuotationDamageFixTypeHelper.Normalize(info.FixTypeKey)
                ?? QuotationDamageFixTypeHelper.Normalize(info.FixTypeName);

            if (normalizedFixType is null && !string.IsNullOrWhiteSpace(info.FixTypeKey))
            {
                normalizedFixType = QuotationDamageFixTypeHelper.ResolveDisplayName(info.FixTypeKey);
            }

            if (normalizedFixType is null && !string.IsNullOrWhiteSpace(info.FixTypeName))
            {
                normalizedFixType = QuotationDamageFixTypeHelper.ResolveDisplayName(info.FixTypeName);
            }

            var storedFixType = !string.IsNullOrWhiteSpace(info.FixTypeName)
                ? QuotationDamageFixTypeHelper.ResolveDisplayName(info.FixTypeName)
                : normalizedFixType;

            if (string.IsNullOrWhiteSpace(storedFixType) && !string.IsNullOrWhiteSpace(info.FixTypeKey))
            {
                storedFixType = QuotationDamageFixTypeHelper.ResolveDisplayName(info.FixTypeKey);
            }

            if (!string.Equals(photo.FixType, storedFixType, StringComparison.Ordinal))
            {
                photo.FixType = storedFixType;
                updated = true;
            }
        }

        if (updated)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 將簽名照片標記為簽名用途，方便舊系統辨識。
    /// </summary>
    private async Task MarkSignaturePhotoAsync(string? signaturePhotoUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(signaturePhotoUid);
        if (normalizedUid is null)
        {
            return;
        }

        var photo = await _context.PhotoData
            .FirstOrDefaultAsync(entity => entity.PhotoUid == normalizedUid, cancellationToken);

        if (photo is null)
        {
            return;
        }

        var updated = false;

        if (!string.Equals(photo.Comment, "簽名", StringComparison.Ordinal))
        {
            photo.Comment = "簽名";
            updated = true;
        }

        if (!string.Equals(photo.PhotoShapeShow, "簽名", StringComparison.Ordinal))
        {
            photo.PhotoShapeShow = "簽名";
            updated = true;
        }

        if (!string.Equals(photo.FixType, "簽名", StringComparison.Ordinal))
        {
            photo.FixType = "簽名";
            updated = true;
        }

        if (updated)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 依據照片主檔補齊傷痕欄位，兼容尚未寫入 remark JSON 的舊資料。
    /// </summary>
    private async Task PopulateDamageFixTypesAsync(
        IEnumerable<string> photoUids,
        List<QuotationDamageItem> damages,
        CancellationToken cancellationToken)
    {
        if (damages is null || damages.Count == 0)
        {
            return;
        }

        var normalizedUids = photoUids?
            .Select(NormalizeOptionalText)
            .Where(uid => uid is not null)
            .Select(uid => uid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        var photoFixTypes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (normalizedUids.Count > 0)
        {
            var photoRecords = await _context.PhotoData
                .AsNoTracking()
                .Where(photo => photo.PhotoUid != null && normalizedUids.Contains(photo.PhotoUid))
                .Select(photo => new { photo.PhotoUid, photo.FixType })
                .ToListAsync(cancellationToken);

            photoFixTypes = photoRecords.ToDictionary(
                photo => photo.PhotoUid!,
                photo => NormalizeOptionalText(photo.FixType),
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            string? resolvedFixType = null;

            foreach (var uid in EnumerateDamagePhotoUids(damage))
            {
                if (uid is null)
                {
                    continue;
                }

                if (!photoFixTypes.TryGetValue(uid, out var fixTypeValue) || string.IsNullOrWhiteSpace(fixTypeValue))
                {
                    continue;
                }

                var normalized = QuotationDamageFixTypeHelper.Normalize(fixTypeValue)
                    ?? QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeValue);

                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    resolvedFixType = normalized;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(resolvedFixType))
            {
                damage.DisplayFixType = resolvedFixType;
                damage.FixTypeName = resolvedFixType;
            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
        }
    }

    private static string? DetermineOverallFixType(IEnumerable<QuotationDamageItem> damages)
    {
        if (damages is null)
        {
            return null;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            var normalized = QuotationDamageFixTypeHelper.Normalize(damage.FixType)
                ?? QuotationDamageFixTypeHelper.Normalize(damage.FixTypeName)
                ?? QuotationDamageFixTypeHelper.Normalize(damage.DisplayFixType);

            if (normalized is null && !string.IsNullOrWhiteSpace(damage.DisplayFixType))
            {
                normalized = QuotationDamageFixTypeHelper.ResolveDisplayName(damage.DisplayFixType);
            }

            if (normalized is null && !string.IsNullOrWhiteSpace(damage.FixTypeName))
            {
                normalized = QuotationDamageFixTypeHelper.ResolveDisplayName(damage.FixTypeName);
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            counts.TryGetValue(normalized, out var current);
            counts[normalized] = current + 1;
        }

        if (counts.Count == 0)
        {
            return null;
        }

        // 依據原始排序規則建立排序後清單，確保統計結果與舊有邏輯一致。
        var orderedFixTypes = counts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => QuotationDamageFixTypeHelper.ResolveOrderIndex(kvp.Key))
            .ToList();

        // 先收集非「其他」類型，再將「其他」放到最後，方便閱讀。
        var prioritizedFixTypes = new List<string>();
        var fallbackFixTypes = new List<string>();

        foreach (var kvp in orderedFixTypes)
        {
            if (string.Equals(kvp.Key, "其他", StringComparison.Ordinal))
            {
                // 將「其他」留待最後串接，避免混淆主要修復方式。
                fallbackFixTypes.Add(kvp.Key);
                continue;
            }

            prioritizedFixTypes.Add(kvp.Key);
        }

        // 合併主要與備用清單，若清單為空則直接回傳 null。
        var mergedFixTypes = prioritizedFixTypes
            .Concat(fallbackFixTypes)
            .ToList();

        if (mergedFixTypes.Count == 0)
        {
            return null;
        }

        // 以「、」串接所有修復方式，形成「凹痕、美容」等多重分類文字描述。
        return string.Join("、", mergedFixTypes);
    }

    private async Task<List<QuotationDamageItem>> NormalizeDamagesWithPhotoDataAsync(
        string? quotationUid,
        List<QuotationDamageItem> damages,
        string? signaturePhotoUid,
        CancellationToken cancellationToken)
    {
        _ = signaturePhotoUid;
        // 簽名圖片不再自動從照片清單排除，此參數保留僅供相容舊呼叫端。

        var normalizedQuotationUid = NormalizeOptionalText(quotationUid);
        if (normalizedQuotationUid is null)
        {
            return damages;
        }

        var quotationFixTypeRaw = await _context.Quatations
            .AsNoTracking()
            .Where(quotation => quotation.QuotationUid == normalizedQuotationUid)
            .Select(quotation => quotation.FixType)
            .FirstOrDefaultAsync(cancellationToken);

        var fallbackFixType = ExtractPrimaryQuotationFixType(quotationFixTypeRaw);

        var photos = await _context.PhotoData
            .AsNoTracking()
            .Where(photo => photo.QuotationUid == normalizedQuotationUid)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return damages;
        }

        if (damages.Count == 0)
        {
            return BuildDamagesFromPhotoData(photos, fallbackFixType);
        }

        EnrichDamagesFromPhotoData(damages, photos, fallbackFixType);
        return damages;
    }

    /// <summary>
    /// 由照片資料建立傷痕清單，供舊資料回傳使用。
    /// </summary>
    private static List<QuotationDamageItem> BuildDamagesFromPhotoData(IEnumerable<PhotoDatum> photos, string? fallbackFixType)
    {
        var result = new List<QuotationDamageItem>();

        foreach (var photo in photos)
        {
            var photoUid = NormalizeOptionalText(photo?.PhotoUid);
            if (photoUid is null)
            {
                continue;
            }

            var storedFixType = NormalizeOptionalText(photo?.FixType);
            var normalizedFixType = QuotationDamageFixTypeHelper.Normalize(storedFixType);
            // 若照片主檔未帶出維修類型，改用估價單紀錄的第一個維修類型補值。
            var fallbackDisplay = string.IsNullOrWhiteSpace(storedFixType) && !string.IsNullOrWhiteSpace(fallbackFixType)
                ? QuotationDamageFixTypeHelper.ResolveDisplayName(fallbackFixType)
                : null;
            var fixTypeDisplay = normalizedFixType
                ?? fallbackDisplay
                ?? (string.IsNullOrWhiteSpace(storedFixType)
                    ? null
                    : QuotationDamageFixTypeHelper.ResolveDisplayName(storedFixType));

            var damage = new QuotationDamageItem
            {
                DisplayPhoto = photoUid,
                DisplayPosition = photo?.Posion,
                DisplayDentStatus = photo?.PhotoShapeShow,
                DisplayDescription = photo?.Comment,
                DisplayEstimatedAmount = photo?.Cost,
                DisplayFixType = fixTypeDisplay,
                FixTypeName = fixTypeDisplay
            };

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage, fallbackFixType);

            result.Add(damage);
        }

        return result;
    }

    /// <summary>
    /// 使用照片主檔補齊現有傷痕欄位，避免資料遺失。
    /// </summary>
    private static void EnrichDamagesFromPhotoData(List<QuotationDamageItem> damages, IReadOnlyCollection<PhotoDatum> photos, string? fallbackFixType)
    {
        if (damages.Count == 0 || photos.Count == 0)
        {
            return;
        }

        var photoLookup = photos
            .Where(photo => !string.IsNullOrWhiteSpace(photo.PhotoUid))
            .ToDictionary(photo => photo.PhotoUid!, photo => photo, StringComparer.OrdinalIgnoreCase);

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            foreach (var photoUid in EnumerateDamagePhotoUids(damage))
            {
                if (photoUid is null || !photoLookup.TryGetValue(photoUid, out var photo))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(damage.DisplayPosition) && !string.IsNullOrWhiteSpace(photo.Posion))
                {
                    damage.DisplayPosition = photo.Posion;
                }

                if (string.IsNullOrWhiteSpace(damage.DisplayDentStatus) && !string.IsNullOrWhiteSpace(photo.PhotoShapeShow))
                {
                    damage.DisplayDentStatus = photo.PhotoShapeShow;
                }

                if (string.IsNullOrWhiteSpace(damage.DisplayDescription) && !string.IsNullOrWhiteSpace(photo.Comment))
                {
                    damage.DisplayDescription = photo.Comment;
                }

                if (!damage.DisplayEstimatedAmount.HasValue && photo.Cost.HasValue)
                {
                    damage.DisplayEstimatedAmount = photo.Cost;
                }

                if (!string.IsNullOrWhiteSpace(photo.FixType))
                {
                    var normalizedFixType = QuotationDamageFixTypeHelper.Normalize(photo.FixType)
                        ?? QuotationDamageFixTypeHelper.ResolveDisplayName(photo.FixType);

                    damage.DisplayFixType = normalizedFixType;
                    damage.FixTypeName = normalizedFixType;
                }
                else if (!string.IsNullOrWhiteSpace(fallbackFixType))
                {
                    // 估價單既有維修類型可作為空白照片紀錄的預設值，避免前端顯示空白類型。
                    var normalizedFallback = QuotationDamageFixTypeHelper.ResolveDisplayName(fallbackFixType);
                    damage.DisplayFixType ??= normalizedFallback;
                    damage.FixTypeName ??= normalizedFallback;
                }

            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage, fallbackFixType);
        }
    }

    /// <summary>
    /// 逐一列出傷痕項目所對應的照片識別碼。
    /// </summary>
    private static IEnumerable<string?> EnumerateDamagePhotoUids(QuotationDamageItem damage)
    {
        var primary = NormalizeOptionalText(damage.Photo);
        if (primary is not null)
        {
            yield return primary;
        }
    }

    /// <summary>
    /// 將多組 Like 樣式組合成單一查詢條件，確保 EF Core 能轉換為有效 SQL。
    /// </summary>
    private static Expression<Func<Quatation, bool>> BuildFixTypeLikePredicate(IEnumerable<string> patterns)
    {
        var patternList = patterns?
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (patternList.Count == 0)
        {
            // 無可用樣式時直接回傳永遠為真，避免 Where 條件產生例外。
            return quotation => true;
        }

        var parameter = Expression.Parameter(typeof(Quatation), "quotation");
        var fixTypeProperty = Expression.Property(parameter, nameof(Quatation.FixType));
        var coalesce = Expression.Coalesce(fixTypeProperty, Expression.Constant(string.Empty, typeof(string)));

        Expression? body = null;

        foreach (var pattern in patternList)
        {
            // 透過 EF.Functions.Like 產生 SQL Like 條件，確保資料庫可利用索引搜尋。
            var likeCall = Expression.Call(
                typeof(DbFunctionsExtensions),
                nameof(DbFunctionsExtensions.Like),
                Type.EmptyTypes,
                Expression.Constant(EF.Functions),
                coalesce,
                Expression.Constant(pattern));

            body = body is null ? likeCall : Expression.OrElse(body, likeCall);
        }

        return Expression.Lambda<Func<Quatation, bool>>(body!, parameter);
    }

    /// <summary>
    /// 從估價單紀錄的維修類型字串中，擷取第一個可辨識的維修類型做為預設值。
    /// </summary>
    private static string? ExtractPrimaryQuotationFixType(string? quotationFixType)
    {
        if (string.IsNullOrWhiteSpace(quotationFixType))
        {
            return null;
        }

        // 估價單歷史資料可能以「、」或逗號分隔多個維修類型，因此先切割後逐一檢查。
        var parts = quotationFixType
            .Split(new[] { '、', ',', ';', '，' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        foreach (var part in parts)
        {
            var normalized = QuotationDamageFixTypeHelper.Normalize(part);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        foreach (var part in parts)
        {
            if (string.Equals(part, "簽名", StringComparison.Ordinal))
            {
                return "簽名";
            }
        }

        // 若皆無法正規化，仍回傳第一個值的顯示名稱，確保前端能顯示內容。
        return QuotationDamageFixTypeHelper.ResolveDisplayName(parts[0]);
    }

    /// <summary>
    /// 將完整傷痕資料轉換為精簡輸出，僅保留前端需要的欄位。
    /// </summary>
    private static List<QuotationDamageSummary> BuildDamageSummaries(IEnumerable<QuotationDamageItem> damages)
    {
        var summaries = new List<QuotationDamageSummary>();

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
            var fixTypeKey = QuotationDamageFixTypeHelper.DetermineGroupKey(damage.FixType);
            var fixTypeName = !string.IsNullOrWhiteSpace(damage.FixTypeName)
                ? damage.FixTypeName
                : QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeKey);
            var primaryPhotoUid = NormalizeOptionalText(ExtractPrimaryPhotoUid(damage));
            summaries.Add(new QuotationDamageSummary
            {
                Photo = primaryPhotoUid,
                Position = NormalizeOptionalText(damage.Position),
                DentStatus = NormalizeOptionalText(damage.DentStatus),
                Description = NormalizeOptionalText(damage.Description),
                EstimatedAmount = damage.EstimatedAmount,
                FixType = NormalizeOptionalText(damage.FixType) ?? fixTypeKey,
                FixTypeName = fixTypeName
            });
        }

        return summaries;
    }

    private static QuotationPhotoSummaryCollection BuildPhotoSummaryCollection(IEnumerable<QuotationDamageItem> damages)
    {
        var summaries = BuildDamageSummaries(damages);
        var result = new QuotationPhotoSummaryCollection();

        foreach (var summary in summaries)
        {
            if (summary is null)
            {
                continue;
            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(summary);
            var groupKey = QuotationDamageFixTypeHelper.DetermineGroupKey(summary.FixType);

            switch (groupKey)
            {
                case "凹痕":
                    result.Dent.Add(summary);
                    break;
                case "美容":
                    result.Beauty.Add(summary);
                    break;
                case "板烤":
                    result.Paint.Add(summary);
                    break;
                default:
                    result.Other.Add(summary);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// 依主要照片標記決定輸出照片識別碼，若無標記則使用第一筆資料。
    /// </summary>
    private static string? ExtractPrimaryPhotoUid(QuotationDamageItem damage)
    {
        return NormalizeOptionalText(damage.Photo);
    }

    /// <summary>
    /// 精簡車體確認單資料，移除標註圖片並保留簽名 PhotoUID 供前端呈現。
    /// </summary>
    private static QuotationCarBodyConfirmationResponse? SimplifyCarBodyConfirmation(QuotationCarBodyConfirmation? source)
    {
        if (source is null)
        {
            return null;
        }

        var markers = source.DamageMarkers is { Count: > 0 }
            ? source.DamageMarkers
                .Select(marker => new QuotationCarBodyDamageMarker
                {
                    Start = new QuotationCarBodyMarkerPoint
                    {
                        X = marker?.Start?.X,
                        Y = marker?.Start?.Y
                    },
                    End = new QuotationCarBodyMarkerPoint
                    {
                        X = marker?.End?.X,
                        Y = marker?.End?.Y
                    },
                    HasDent = marker?.HasDent ?? false,
                    HasScratch = marker?.HasScratch ?? false,
                    HasPaintPeel = marker?.HasPaintPeel ?? false,
                    Remark = marker?.Remark
                })
                .ToList()
            : new List<QuotationCarBodyDamageMarker>();

        return new QuotationCarBodyConfirmationResponse
        {
            DamageMarkers = markers,
            SignaturePhotoUid = NormalizeOptionalText(source.SignaturePhotoUid)
                ?? NormalizeOptionalText(source.Signature)
        };
    }

    /// <summary>
    /// 將傷痕資訊轉換成照片註解文字，方便人員辨識。
    /// </summary>
    private static string? BuildDamageComment(string? position, string? dentStatus, string? description, decimal? amount)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(position))
        {
            parts.Add($"位置：{position}");
        }

        if (!string.IsNullOrWhiteSpace(dentStatus))
        {
            parts.Add($"狀況：{dentStatus}");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add($"說明：{description}");
        }

        if (amount.HasValue)
        {
            parts.Add($"預估金額：{amount.Value:0.##}");
        }

        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }

    /// <summary>
    /// 從建立估價單請求中取得傷痕列表，僅使用新版頂層 damages 欄位。
    /// </summary>
    private static List<QuotationDamageItem> ExtractDamageList(CreateQuotationRequest request)
    {
        return request.Photos?.ToDamageList() ?? new List<QuotationDamageItem>();
    }

    /// <summary>
    /// 將服務類別內的傷痕集合轉換為單一清單，供相容性處理使用。
    /// </summary>
    private static List<QuotationDamageItem> FlattenCategoryDamages(QuotationServiceCategoryCollection categories)
    {
        var damages = new List<QuotationDamageItem>();

        foreach (var block in EnumerateCategoryBlocks(categories))
        {
            if (block.Damages is not { Count: > 0 })
            {
                continue;
            }

            foreach (var damage in block.Damages)
            {
                if (damage is not null)
                {
                    damages.Add(damage);
                }
            }
        }

        return damages;
    }

    /// <summary>
    /// 逐一走訪三大類別的資料區塊，方便彙整金額。
    /// </summary>
    private static IEnumerable<QuotationCategoryBlock> EnumerateCategoryBlocks(QuotationServiceCategoryCollection categories)
    {
        if (categories.Dent is not null)
        {
            yield return categories.Dent;
        }

        if (categories.Paint is not null)
        {
            yield return categories.Paint;
        }

        if (categories.Other is not null)
        {
            yield return categories.Other;
        }
    }

    /// <summary>
    /// 確保取得指定類別的資料區塊，若不存在則建立預設實例。
    /// </summary>
    private static QuotationCategoryBlock EnsureCategoryBlock(QuotationServiceCategoryCollection categories, string categoryKey)
    {
        return categoryKey switch
        {
            "dent" => categories.Dent ??= new QuotationCategoryBlock(),
            "paint" => categories.Paint ??= new QuotationCategoryBlock(),
            "other" => categories.Other ??= new QuotationCategoryBlock(),
            _ => categories.Other ??= new QuotationCategoryBlock()
        };
    }

    /// <summary>
    /// 取得指定類別鍵值對應的折扣設定，若不存在則建立預設實例。
    /// </summary>
    private static QuotationMaintenanceCategoryAdjustment EnsureCategoryAdjustment(
        QuotationMaintenanceCategoryAdjustmentCollection adjustments,
        string? categoryKey)
    {
        // 預設回落至 other，避免舊資料缺少維修類型時落在空集合。
        var normalizedKey = string.IsNullOrWhiteSpace(categoryKey)
            ? "other"
            : categoryKey;

        return normalizedKey switch
        {
            "dent" => adjustments.Dent ??= new QuotationMaintenanceCategoryAdjustment(),
            "paint" => adjustments.Paint ??= new QuotationMaintenanceCategoryAdjustment(),
            "other" => adjustments.Other ??= new QuotationMaintenanceCategoryAdjustment(),
            _ => adjustments.Other ??= new QuotationMaintenanceCategoryAdjustment()
        };
    }

    /// <summary>
    /// 將類別索引正規化為 dent / paint / other。
    /// </summary>
    private static string? NormalizeCategoryKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return key.Trim().ToLowerInvariant() switch
        {
            "dent" or "凹痕" => "dent",
            "paint" or "鈑烤" or "板烤" => "paint",
            "beauty" or "美容" => "other",
            "other" or "其他" => "other",
            _ => null
        };
    }

    /// <summary>
    /// 透過維修類型解析類別鍵值，供舊資料回填至正確欄位。
    /// </summary>
    private static string? ResolveCategoryKeyFromFixType(string? fixType)
    {
        if (string.IsNullOrWhiteSpace(fixType))
        {
            return null;
        }

        // 先以正規化鍵值比對，若失敗則使用顯示名稱再行轉換。
        var normalizedFixType = QuotationDamageFixTypeHelper.Normalize(fixType)
            ?? QuotationDamageFixTypeHelper.ResolveDisplayName(fixType);

        if (string.Equals(normalizedFixType, "美容", StringComparison.Ordinal))
        {
            return "beauty";
        }

        return NormalizeCategoryKey(normalizedFixType);
    }

    /// <summary>
    /// 從樣本清單中隨機挑選一筆資料，若清單為空則回傳 null。
    /// </summary>
    private static T? PickRandomOrDefault<T>(IReadOnlyList<T> source, Random random)
    {
        if (source is null || source.Count == 0)
        {
            return default;
        }

        return source[random.Next(source.Count)];
    }

    /// <summary>
    /// 建立測試資料用的預約方式文字，提供多樣化範例方便前端驗證畫面。
    /// </summary>
    private static string BuildBookMethodText(Random random)
    {
        return TestBookMethodSamples[random.Next(TestBookMethodSamples.Length)];
    }

    /// <summary>
    /// 建立隨機傷痕資料，並盡量帶入既有照片作為測試素材。
    /// </summary>
    private static List<QuotationDamageItem> BuildRandomDamages(IReadOnlyList<PhotoEntity> photoSamples, Random random)
    {
        var damageCount = random.Next(1, 4);
        var damages = new List<QuotationDamageItem>();

        for (var i = 0; i < damageCount; i++)
        {
            var damage = new QuotationDamageItem
            {
                DisplayPosition = TestDamagePositions[random.Next(TestDamagePositions.Length)],
                DisplayDentStatus = TestDamageStatuses[random.Next(TestDamageStatuses.Length)],
                DisplayDescription = TestDamageDescriptions[random.Next(TestDamageDescriptions.Length)],
                DisplayEstimatedAmount = Math.Round(1500m + (decimal)random.NextDouble() * 4000m, 0)
            };

            var fixTypeKey = QuotationDamageFixTypeHelper.CanonicalOrder[random.Next(QuotationDamageFixTypeHelper.CanonicalOrder.Count)];
            damage.DisplayFixType = fixTypeKey;
            damage.FixTypeName = QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeKey);

            var photoUid = PickRandomPhotoUid(photoSamples, random);
            if (photoUid is not null)
            {
                damage.DisplayPhoto = photoUid;
            }

            damages.Add(damage);
        }

        return damages;
    }

    private static QuotationPhotoRequestCollection GroupDamagesForRequest(IEnumerable<QuotationDamageItem> damages)
    {
        var collection = new QuotationPhotoRequestCollection();

        if (damages is null)
        {
            return collection;
        }

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(damage);
            var groupKey = QuotationDamageFixTypeHelper.DetermineGroupKey(damage.FixType);

            switch (groupKey)
            {
                case "凹痕":
                    collection.Dent.Add(damage);
                    break;
                case "美容":
                    collection.Beauty.Add(damage);
                    break;
                case "板烤":
                    collection.Paint.Add(damage);
                    break;
                default:
                    collection.Other.Add(damage);
                    break;
            }
        }

        return collection;
    }

    /// <summary>
    /// 建立隨機車體確認單資料，產生示意座標與簽名圖片。
    /// </summary>
    private static QuotationCarBodyConfirmation BuildRandomCarBodyConfirmation(IReadOnlyList<PhotoEntity> photoSamples, Random random)
    {
        var markerCount = random.Next(1, 3);
        var markers = new List<QuotationCarBodyDamageMarker>();

        for (var i = 0; i < markerCount; i++)
        {
            markers.Add(new QuotationCarBodyDamageMarker
            {
                Start = new QuotationCarBodyMarkerPoint
                {
                    X = Math.Round(random.NextDouble(), 2),
                    Y = Math.Round(random.NextDouble(), 2)
                },
                End = new QuotationCarBodyMarkerPoint
                {
                    X = Math.Round(random.NextDouble(), 2),
                    Y = Math.Round(random.NextDouble(), 2)
                },
                HasDent = true,
                HasScratch = random.Next(2) == 0,
                HasPaintPeel = random.Next(2) == 0,
                Remark = $"測試標記 {i + 1}"
            });
        }

        return new QuotationCarBodyConfirmation
        {
            SignaturePhotoUid = PickRandomPhotoUid(photoSamples, random) ?? BuildFallbackUid("Ph"),
            DamageMarkers = markers
        };
    }

    /// <summary>
    /// 建立隨機維修設定資料，模擬一般估價流程會填入的欄位內容。
    /// </summary>
    private static CreateQuotationMaintenanceInfo BuildRandomMaintenance(string fixTypeKey, Random random)
    {
        var percentageDiscount = random.Next(0, 2) == 0
            ? (decimal?)null
            : Math.Round((decimal)random.NextDouble() * 15m, 1);

        var dentOtherFee = Math.Round((decimal)random.NextDouble() * 600m, 0);
        var paintOtherFee = Math.Round((decimal)random.NextDouble() * 800m, 0);
        var otherOtherFee = Math.Round((decimal)random.NextDouble() * 400m, 0);
        var paintDiscount = percentageDiscount.HasValue
            ? Math.Round(Math.Max(percentageDiscount.Value - 2m, 0m), 1)
            : (decimal?)null;

        var normalizedFixType = QuotationDamageFixTypeHelper.Normalize(fixTypeKey)
            ?? QuotationDamageFixTypeHelper.ResolveDisplayName(fixTypeKey);
        return new CreateQuotationMaintenanceInfo
        {
            ReserveCar = random.Next(2) == 0,
            ApplyCoating = random.Next(2) == 0,
            ApplyWrapping = random.Next(2) == 0,
            HasRepainted = random.Next(2) == 0,
            NeedToolEvaluation = random.Next(2) == 0,
            OtherFee = dentOtherFee + paintOtherFee + otherOtherFee,
            RoundingDiscount = Math.Round((decimal)random.NextDouble() * 300m, 0),
            PercentageDiscount = percentageDiscount,
            DiscountReason = percentageDiscount.HasValue && percentageDiscount.Value > 0
                ? "測試折扣：系統隨機產生"
                : null,
            EstimatedRepairDays = random.Next(0, 3),
            EstimatedRepairHours = random.Next(1, 8),
            EstimatedRestorationPercentage = Math.Round(80m + (decimal)random.NextDouble() * 20m, 0),
            FixTimeHour = random.Next(1, 6),
            FixTimeMin = random.Next(0, 4) * 15,
            FixExpectDay = random.Next(0, 3),
            FixExpectHour = random.Next(0, 24),
            Remark = "此為隨機測試資料，正式使用前請再次確認。",
            CategoryAdjustments = new QuotationMaintenanceCategoryAdjustmentCollection
            {
                Dent = new QuotationMaintenanceCategoryAdjustment
                {
                    OtherFee = dentOtherFee,
                    PercentageDiscount = percentageDiscount,
                    DiscountReason = percentageDiscount.HasValue ? "凹痕折扣：測試資料" : null
                },
                Paint = new QuotationMaintenanceCategoryAdjustment
                {
                    OtherFee = paintOtherFee,
                    PercentageDiscount = paintDiscount,
                    DiscountReason = paintDiscount.HasValue ? "板烤折扣：測試資料" : null
                },
                Other = new QuotationMaintenanceCategoryAdjustment
                {
                    OtherFee = otherOtherFee,
                    PercentageDiscount = null,
                    DiscountReason = null
                }
            }
        };
    }

    /// <summary>
    /// 建立技師摘要資訊，方便前端顯示測試資料對應人員。
    /// </summary>
    private static CreateQuotationTestEntitySummary CreateTechnicianSummary(TechnicianEntity technician)
    {
        var name = NormalizeOptionalText(technician.TechnicianName) ?? "測試技師";
        var storeName = NormalizeOptionalText(technician.Store?.StoreName);
        var jobTitle = NormalizeOptionalText(technician.JobTitle);

        // 組合職稱與門市資訊，讓前端可直接顯示完整介紹文字。
        var descriptionParts = new List<string>();
        if (jobTitle is not null)
        {
            descriptionParts.Add($"職稱：{jobTitle}");
        }

        if (storeName is not null)
        {
            descriptionParts.Add($"所屬門市：{storeName}");
        }

        return new CreateQuotationTestEntitySummary
        {
            Uid = technician.TechnicianUid,
            Name = name,
            Description = descriptionParts.Count == 0 ? null : string.Join("，", descriptionParts)
        };
    }

    /// <summary>
    /// 建立門市摘要資訊。
    /// </summary>
    private static CreateQuotationTestEntitySummary CreateStoreSummary(StoreEntity store)
    {
        var name = NormalizeOptionalText(store.StoreName) ?? "測試門市";
        return new CreateQuotationTestEntitySummary
        {
            Uid = store.StoreUid,
            Name = name,
            Description = "估價單將以此門市建立"
        };
    }

    /// <summary>
    /// 建立客戶摘要資訊，整合電話與地區資訊。
    /// </summary>
    private static CreateQuotationTestEntitySummary CreateCustomerSummary(CustomerEntity customer)
    {
        var name = NormalizeOptionalText(customer.Name) ?? "測試客戶";
        var descriptionParts = new List<string>();

        var phone = NormalizeOptionalText(customer.Phone);
        if (phone is not null)
        {
            descriptionParts.Add($"電話：{phone}");
        }

        var county = NormalizeOptionalText(customer.County);
        var township = NormalizeOptionalText(customer.Township);
        var region = string.Concat(county ?? string.Empty, township ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(region))
        {
            descriptionParts.Add($"地區：{region}");
        }

        var source = NormalizeOptionalText(customer.Source);
        if (source is not null)
        {
            descriptionParts.Add($"來源：{source}");
        }

        return new CreateQuotationTestEntitySummary
        {
            Uid = customer.CustomerUid,
            Name = name,
            Description = descriptionParts.Count > 0 ? string.Join("，", descriptionParts) : null
        };
    }

    /// <summary>
    /// 建立車輛摘要資訊，包含車牌、品牌與顏色描述。
    /// </summary>
    private static CreateQuotationTestEntitySummary CreateCarSummary(CarEntity car)
    {
        var plate = NormalizeOptionalText(car.CarNo) ?? "測試車牌";
        var descriptionParts = new List<string>();

        var brand = NormalizeOptionalText(car.Brand);
        var model = NormalizeOptionalText(car.Model);
        var brandModelParts = new List<string>();
        if (brand is not null)
        {
            brandModelParts.Add(brand);
        }

        if (model is not null)
        {
            brandModelParts.Add(model);
        }

        if (brandModelParts.Count > 0)
        {
            descriptionParts.Add($"車型：{string.Join(" ", brandModelParts)}");
        }

        var color = NormalizeOptionalText(car.Color);
        if (color is not null)
        {
            descriptionParts.Add($"車色：{color}");
        }

        return new CreateQuotationTestEntitySummary
        {
            Uid = car.CarUid,
            Name = plate,
            Description = descriptionParts.Count > 0 ? string.Join("，", descriptionParts) : null
        };
    }

    /// <summary>
    /// 建立測試頁面提示訊息，提供前端顯示於 UI。
    /// </summary>
    private static List<string> BuildTestNotes(CreateQuotationRequest draft, bool usedExistingData)
    {
        var notes = new List<string>
        {
            "本回傳資料由系統隨機產生，僅供測試新增估價單頁面使用。"
        };

        notes.Add(usedExistingData
            ? "部分欄位取用資料庫既有資料，請於送出前確認是否符合測試情境。"
            : "目前資料庫缺少樣本，所有欄位皆由系統隨機填入。");

        if (draft.Store?.BookMethod is not null)
        {
            notes.Add($"預約方式：{draft.Store.BookMethod}");
        }

        if (draft.Store?.ReservationDate is not null)
        {
            notes.Add($"預約日期：{draft.Store.ReservationDate:yyyy/MM/dd HH:mm}");
        }

        var aggregatedFixType = DetermineOverallFixType(draft.Photos?.ToDamageList() ?? new List<QuotationDamageItem>());
        if (!string.IsNullOrWhiteSpace(aggregatedFixType))
        {
            notes.Add($"維修類型：{aggregatedFixType}");
        }

        return notes;
    }

    /// <summary>
    /// 從照片樣本中挑選可用的 PhotoUID。
    /// </summary>
    private static string? PickRandomPhotoUid(IReadOnlyList<PhotoEntity> photoSamples, Random random)
    {
        if (photoSamples is null || photoSamples.Count == 0)
        {
            return null;
        }

        var candidate = photoSamples[random.Next(photoSamples.Count)];
        return NormalizeOptionalText(candidate.PhotoUid);
    }

    /// <summary>
    /// 為測試資料建立具有辨識性的臨時 UID。
    /// </summary>
    private static string BuildFallbackUid(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():D}".ToUpperInvariant();
    }

    /// <summary>
    /// 處理必填欄位，若為空值則拋出例外提示呼叫端補齊。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 處理可選文字欄位，若為空白則回傳 null。
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// 正規化操作人員名稱，避免寫入空白。
    /// </summary>
    private static string NormalizeOperator(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return "UnknownUser";
        }

        return operatorName.Trim();
    }

    /// <summary>
    /// 轉換車牌為僅保留數字與字母的大寫格式，移除中間的連字號或特殊符號。
    /// </summary>
    private static string NormalizeLicensePlate(string plate)
    {
        var filtered = new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(filtered))
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "車牌號碼格式不正確，請重新輸入。");
        }

        return filtered;
    }

    /// <summary>
    /// 將電話轉換為僅包含數字的查詢字串。
    /// </summary>
    private static string? NormalizePhoneQuery(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    /// <summary>
    /// 將布林旗標轉換為資料庫慣用的 Y / N 字元。
    /// </summary>
    private static string ConvertBooleanToFlag(bool? value)
    {
        // 將布林轉為舊系統慣用的「1 / 空白」格式，方便沿用既有報表邏輯。
        return value.HasValue && value.Value ? "1" : string.Empty;
    }

    /// <summary>
    /// 將資料庫紀錄的文字旗標轉回布林值，支援 Y/N、True/False、是/否等常見寫法。
    /// </summary>
    private static bool? ParseBooleanFlag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "y" or "yes" or "true" or "1" or "是" or "有" => true,
            "n" or "no" or "false" or "0" or "否" or "無" => false,
            _ => null
        };
    }

    /// <summary>
    /// 將可選的日期欄位正規化為 DateOnly，避免時間資訊影響資料庫儲存。
    /// </summary>
    private static DateOnly? NormalizeOptionalDate(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        // 僅保留日期部分，確保與資料庫 DateOnly 欄位一致。
        return DateOnly.FromDateTime(value.Value.Date);
    }

    /// <summary>
    /// 將 DateOnly 轉回 DateTime，方便回傳前端顯示。
    /// </summary>
    private static DateTime? ConvertDateOnlyToDateTime(DateOnly? value)
    {
        return value?.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>
    /// 取得台北當地時間，並在系統缺少時區資訊時退回 +8 小時補正。
    /// </summary>
    private static DateTime GetTaipeiNow()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var zoneId in TaipeiTimeZoneIds)
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                // 若伺服器不支援該時區 ID，繼續嘗試下一組。
            }
            catch (InvalidTimeZoneException)
            {
                // 若時區設定異常，同樣嘗試下一組 ID。
            }
        }

        return utcNow.AddHours(8);
    }

    // ---------- 生命週期 ----------
    // 服務由 DI 管理生命週期，無需額外釋放資源。

    /// <summary>
    /// remark 的序列化包裝，保留原始備註與擴充資料。
    /// </summary>
    private class QuotationRemarkEnvelope
    {
        /// <summary>
        /// remark 版本，預留後續擴充使用。
        /// </summary>
        public int Version { get; set; } = 2;

        /// <summary>
        /// 純文字備註內容。
        /// </summary>
        public string? PlainRemark { get; set; }

        /// <summary>
        /// 擴充資料集合。
        /// </summary>
        public QuotationExtraData? Extra { get; set; }
    }

    /// <summary>
    /// 估價單擴充資料，包含傷痕、車體確認與維修補充資訊。
    /// </summary>
    private class QuotationExtraData
    {
        /// <summary>
        /// 各服務類別資訊。
        /// </summary>
        public QuotationServiceCategoryCollection? ServiceCategories { get; set; }

        /// <summary>
        /// 類別金額總覽。
        /// </summary>
        public QuotationCategoryTotal? CategoryTotal { get; set; }

        /// <summary>
        /// 車體確認單資料。
        /// </summary>
        public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

        /// <summary>
        /// 傷痕細項列表，配合新版格式與服務類別拆分存放。
        /// </summary>
        public List<QuotationDamageItem>? Damages { get; set; }

        /// <summary>
        /// 依三大服務類別拆分的額外費用與折扣資訊。
        /// </summary>
        public QuotationMaintenanceCategoryAdjustmentCollection? CategoryAdjustments { get; set; }

        /// <summary>
        /// 其他估價費用。
        /// </summary>
        public decimal? OtherFee { get; set; }

        /// <summary>
        /// 零頭折扣金額，搭配折扣百分比調整估價總額。
        /// </summary>
        public decimal? RoundingDiscount { get; set; }

        /// <summary>
        /// 折扣百分比，單位為百分比數值（例如 10 代表 10%）。
        /// </summary>
        public decimal? PercentageDiscount { get; set; }

        /// <summary>
        /// 折扣原因描述，紀錄折扣依據或客戶身份。
        /// </summary>
        public string? DiscountReason { get; set; }

        /// <summary>
        /// 預估施工所需天數。
        /// </summary>
        public int? EstimatedRepairDays { get; set; }

        /// <summary>
        /// 預估施工所需時數。
        /// </summary>
        public int? EstimatedRepairHours { get; set; }

        /// <summary>
        /// 預估修復完成度（百分比）。
        /// </summary>
        public decimal? EstimatedRestorationPercentage { get; set; }

        /// <summary>
        /// 建議改採鈑烤的原因。
        /// </summary>
        public string? SuggestedPaintReason { get; set; }

        /// <summary>
        /// 無法修復時的原因。
        /// </summary>
        public string? UnrepairableReason { get; set; }
    }

    /// <summary>
    /// 類別折扣正規化結果，保留原始是否提供過欄位資訊。
    /// </summary>
    private sealed class MaintenanceAdjustmentNormalizationResult
    {
        /// <summary>
        /// 正規化後的類別折扣設定。
        /// </summary>
        public QuotationMaintenanceCategoryAdjustmentCollection Adjustments { get; init; } = new();

        /// <summary>
        /// 是否原本就有傳入類別資料，便於後續判斷顯示文字。
        /// </summary>
        public bool HasExplicitAdjustments { get; init; }
    }

    /// <summary>
    /// 維修金額計算摘要，整合折扣、費用與估價結果。
    /// </summary>
    private sealed class MaintenanceFinancialSummary
    {
        /// <summary>
        /// 正規化後的類別折扣設定。
        /// </summary>
        public QuotationMaintenanceCategoryAdjustmentCollection Adjustments { get; init; } = new();

        /// <summary>
        /// 是否存在任一類別設定資料，用於決定是否輸出至 remark。
        /// </summary>
        public bool HasAdjustmentData { get; init; }

        /// <summary>
        /// 是否確實帶入新版類別調整資料，供判斷回傳格式使用。
        /// </summary>
        public bool HasExplicitAdjustments { get; init; }

        /// <summary>
        /// 額外費用的加總結果。
        /// </summary>
        public decimal? OtherFee { get; init; }

        /// <summary>
        /// 加權後的有效折扣百分比。
        /// </summary>
        public decimal? EffectivePercentageDiscount { get; init; }

        /// <summary>
        /// 彙整後的折扣原因。
        /// </summary>
        public string? DiscountReason { get; init; }

        /// <summary>
        /// 經過折扣與零頭折扣後的估價金額。
        /// </summary>
        public decimal? Valuation { get; init; }
    }
}
