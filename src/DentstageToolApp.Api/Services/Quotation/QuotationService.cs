using DentstageToolApp.Api.Quotations;
using DentstageToolApp.Api.Services.Photo;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using CarEntity = DentstageToolApp.Infrastructure.Entities.Car;
using CustomerEntity = DentstageToolApp.Infrastructure.Entities.Customer;
using FixTypeEntity = DentstageToolApp.Infrastructure.Entities.FixType;
using StoreEntity = DentstageToolApp.Infrastructure.Entities.Store;
using TechnicianEntity = DentstageToolApp.Infrastructure.Entities.Technician;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // 篩選維修類型，優先以識別碼比對，維持舊有欄位作為相容邏輯。
        if (!string.IsNullOrWhiteSpace(query.FixType))
        {
            var fixTypeFilter = query.FixType.Trim();
            quotationsQuery = quotationsQuery.Where(q =>
                q.FixTypeUid == fixTypeFilter || q.FixType == fixTypeFilter);
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
            join fixType in _context.FixTypes.AsNoTracking()
                on quotation.FixTypeUid equals fixType.FixTypeUid into fixTypeGroup
            from fixType in fixTypeGroup.DefaultIfEmpty()
            join store in _context.Stores.AsNoTracking()
                on quotation.StoreUid equals store.StoreUid into storeGroup
            from store in storeGroup.DefaultIfEmpty()
            orderby quotation.CreationTimestamp ?? DateTime.MinValue descending,
                quotation.QuotationNo descending
            select new { quotation, brand, model, fixType, store };

        // 先取出分頁後的原始資料集合，避免一次載入過多資料。
        var pagedSource = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 彙整當前頁面所需的使用者 UID，僅針對有值的項目進行查詢，降低額外資料庫負擔。
        var estimatorUserUids = pagedSource
            .Select(result => NormalizeOptionalText(result.quotation.UserUid))
            .Where(uid => uid is not null)
            .Select(uid => uid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var estimatorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

                estimatorMap[normalizedUid] = normalizedName;
            }
        }

        var items = pagedSource
            .Select(result =>
            {
                var quotation = result.quotation;
                var brand = result.brand;
                var model = result.model;
                var fixType = result.fixType;
                var store = result.store;

                // 優先使用使用者帳號顯示名稱，若查無對應資料則回退為估價單上的 UserName 欄位。
                var estimatorName = quotation.UserName;
                var normalizedEstimatorUid = NormalizeOptionalText(quotation.UserUid);
                if (normalizedEstimatorUid is not null &&
                    estimatorMap.TryGetValue(normalizedEstimatorUid, out var mappedName) &&
                    !string.IsNullOrWhiteSpace(mappedName))
                {
                    estimatorName = mappedName;
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
                    // 門市名稱優先採用主檔資料，若關聯不存在則回落至原欄位。
                    StoreName = store != null ? store.StoreName : quotation.CurrentStatusUser,
                    // 估價人員名稱若查無主檔資料，則使用估價單建立者名稱，維持舊資料相容性。
                    EstimatorName = estimatorName,
                    // 建立人員暫做為製單技師資訊。
                    CreatorName = quotation.CreatedBy,
                    CreatedAt = quotation.CreationTimestamp,
                    // 維修類型若有主檔，回傳主檔名稱，否則回退舊有欄位。
                    FixType = fixType != null ? fixType.FixTypeName : quotation.FixType
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
        var technicianEntity = await GetTechnicianEntityAsync(storeInfo.TechnicianUid, cancellationToken);
        if (technicianEntity is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請選擇有效的估價技師。");
        }

        // 透過技師關聯的門市主檔，自動補齊店鋪名稱等資訊。
        var storeEntity = await GetStoreEntityAsync(technicianEntity, cancellationToken);
        var storeName = NormalizeRequiredText(storeEntity?.StoreName, "店鋪名稱");
        var storeUid = NormalizeRequiredText(ResolveStoreUid(technicianEntity, storeEntity), "門市識別碼");
        var operatorLabel = NormalizeOperator(operatorContext.OperatorName);
        var operatorUid = NormalizeOptionalText(operatorContext.UserUid);
        var estimatorName = NormalizeOptionalText(technicianEntity.TechnicianName) ?? operatorLabel;
        var creatorName = operatorLabel;
        var source = NormalizeRequiredText(storeInfo.Source, "維修來源");

        // ---------- 維修設定處理 ----------
        // 依據維修類型 UID 驗證主檔，並轉換常見布林選項為資料表欄位使用的旗標文字。
        var maintenanceInfo = request.Maintenance ?? new CreateQuotationMaintenanceInfo();
        var fixTypeUid = NormalizeRequiredText(maintenanceInfo.FixTypeUid, "維修類型");
        var fixTypeEntity = await GetFixTypeEntityAsync(fixTypeUid, cancellationToken);
        var fixTypeName = NormalizeOptionalText(maintenanceInfo.FixTypeName)
            ?? NormalizeOptionalText(fixTypeEntity?.FixTypeName)
            ?? fixTypeUid;
        var reserveCarFlag = ConvertBooleanToFlag(maintenanceInfo.ReserveCar);
        var coatingFlag = ConvertBooleanToFlag(maintenanceInfo.ApplyCoating);
        var wrappingFlag = ConvertBooleanToFlag(maintenanceInfo.ApplyWrapping);
        var repaintFlag = ConvertBooleanToFlag(maintenanceInfo.HasRepainted);
        var toolFlag = ConvertBooleanToFlag(maintenanceInfo.NeedToolEvaluation);
        var maintenanceRemark = NormalizeOptionalText(maintenanceInfo.Remark);
        var otherFee = maintenanceInfo.OtherFee;
        var estimatedRepairDays = maintenanceInfo.EstimatedRepairDays;
        var estimatedRepairHours = maintenanceInfo.EstimatedRepairHours;
        var estimatedRestorationPercentage = maintenanceInfo.EstimatedRestorationPercentage;
        var suggestedPaintReason = NormalizeOptionalText(maintenanceInfo.SuggestedPaintReason);
        var unrepairableReason = NormalizeOptionalText(maintenanceInfo.UnrepairableReason);
        var roundingDiscount = maintenanceInfo.RoundingDiscount;
        var percentageDiscount = maintenanceInfo.PercentageDiscount;
        var discountReason = NormalizeOptionalText(maintenanceInfo.DiscountReason);

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

        // 透過車輛主檔補齊車牌、品牌等欄位，並確保車牌為統一的大寫格式。
        var carUid = NormalizeRequiredText(carEntity.CarUid, "車輛識別碼");
        var licensePlate = NormalizeRequiredText(carEntity.CarNo, "車牌號碼").ToUpperInvariant();
        var brand = NormalizeOptionalText(carEntity.Brand);
        var model = NormalizeOptionalText(carEntity.Model);
        var color = NormalizeOptionalText(carEntity.Color);
        var carRemark = NormalizeOptionalText(carEntity.CarRemark);

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
        var customerSource = NormalizeOptionalText(customerEntity.Source);
        var customerRemark = NormalizeOptionalText(customerEntity.ConnectRemark);

        var normalizedDamages = ExtractDamageList(request);

        // 建立日期改由系統產生，減少前端填寫欄位。
        var createdAt = DateTime.UtcNow;
        var quotationDate = DateOnly.FromDateTime(createdAt);
        var phoneQuery = NormalizePhoneQuery(customerPhone);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 系統資料計算區 ----------
        var serialNumber = await GenerateNextSerialNumberAsync(cancellationToken);
        var quotationUid = BuildQuotationUid();
        var quotationNo = BuildQuotationNo(serialNumber, createdAt);

        var extraData = new QuotationExtraData
        {
            CarBodyConfirmation = request.CarBodyConfirmation,
            Damages = normalizedDamages.Count > 0 ? normalizedDamages : null,
            OtherFee = otherFee,
            RoundingDiscount = roundingDiscount,
            PercentageDiscount = percentageDiscount,
            DiscountReason = discountReason,
            EstimatedRepairDays = estimatedRepairDays,
            EstimatedRepairHours = estimatedRepairHours,
            EstimatedRestorationPercentage = estimatedRestorationPercentage,
            SuggestedPaintReason = suggestedPaintReason,
            UnrepairableReason = unrepairableReason
        };

        var remarkPayload = SerializeRemark(maintenanceRemark, extraData);
        var valuation = CalculateTotalAmount(normalizedDamages, otherFee, roundingDiscount, percentageDiscount);

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
            UserUid = operatorUid,
            Date = quotationDate,
            StoreUid = storeUid,
            TechnicianUid = technicianEntity?.TechnicianUid,
            CurrentStatusUser = storeName,
            UserName = estimatorName ?? operatorLabel,
            BookDate = reservationDate,
            FixDate = repairDate,
            Source = source,
            FixTypeUid = fixTypeUid,
            FixType = fixTypeName,
            CarReserved = reserveCarFlag,
            Coat = coatingFlag,
            Envelope = wrappingFlag,
            Paint = repaintFlag,
            ToolTest = toolFlag,
            CarUid = carUid,
            CarNo = licensePlate,
            CarNoInput = licensePlate,
            CarNoInputGlobal = licensePlate,
            Brand = brand,
            Model = model,
            BrandModel = BuildBrandModel(brand, model),
            Color = color,
            CarRemark = carRemark,
            CustomerUid = customerUid,
            Name = customerName,
            Phone = customerPhone,
            PhoneInput = customerPhone,
            PhoneInputGlobal = phoneQuery ?? customerPhone,
            Gender = customerGender,
            CustomerType = customerSource,
            ConnectRemark = customerRemark,
            Remark = remarkPayload,
            Discount = roundingDiscount,
            DiscountPercent = percentageDiscount,
            DiscountReason = discountReason,
            Valuation = valuation
        };

        await _context.Quatations.AddAsync(quotationEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // 將建立流程中帶入的所有 PhotoUID 一次綁定到新建立的估價單。
        var photoUids = CollectPhotoUids(request);
        if (photoUids.Count > 0)
        {
            await _photoService.BindToQuotationAsync(quotationEntity.QuotationUid, photoUids, cancellationToken);
        }

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

        var query = _context.Quatations
            .AsNoTracking()
            .Include(q => q.StoreNavigation)
            .Include(q => q.BrandNavigation)
            .Include(q => q.ModelNavigation)
            .Include(q => q.FixTypeNavigation);

        query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Quatation, Model?>)ApplyQuotationFilter(query, request.QuotationUid, request.QuotationNo);

        var quotation = await query.FirstOrDefaultAsync(cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無符合條件的估價單。");
        }

        var (plainRemark, extraData) = ParseRemark(quotation.Remark);

        // ---------- 估價人員名稱組裝 ----------
        // 預設使用估價單上紀錄的 UserName，若 UserUid 能對應使用者主檔則改採顯示名稱。
        var estimatorName = quotation.UserName;
        var estimatorUid = NormalizeOptionalText(quotation.UserUid);
        if (estimatorUid is not null)
        {
            // 僅在 UID 有值時才進行查詢，避免對舊資料造成額外的資料庫負擔。
            var accountDisplayName = await _context.UserAccounts
                .AsNoTracking()
                .Where(account => account.UserUid == estimatorUid)
                .Select(account => account.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(accountDisplayName))
            {
                estimatorName = accountDisplayName;
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
        var maintenanceRemark = plainRemark;
        var otherFee = extraData?.OtherFee;
        var roundingDiscount = extraData?.RoundingDiscount ?? quotation.Discount;
        var percentageDiscount = extraData?.PercentageDiscount ?? quotation.DiscountPercent;
        var discountReason = NormalizeOptionalText(extraData?.DiscountReason ?? quotation.DiscountReason);
        var estimatedRepairDays = extraData?.EstimatedRepairDays;
        var estimatedRepairHours = extraData?.EstimatedRepairHours;
        var estimatedRestorationPercentage = extraData?.EstimatedRestorationPercentage;
        var suggestedPaintReason = NormalizeOptionalText(extraData?.SuggestedPaintReason);
        var unrepairableReason = NormalizeOptionalText(extraData?.UnrepairableReason);

        return new QuotationDetailResponse
        {
            QuotationUid = quotation.QuotationUid,
            QuotationNo = quotation.QuotationNo,
            Status = quotation.Status,
            CreatedAt = quotation.CreationTimestamp,
            UpdatedAt = quotation.ModificationTimestamp,
            Store = new QuotationStoreInfo
            {
                StoreUid = quotation.StoreUid,
                UserUid = quotation.UserUid,
                StoreName = quotation.StoreNavigation?.StoreName ?? quotation.CurrentStatusUser ?? string.Empty,
                // 估價人員名稱優先顯示使用者主檔資料，若查無對應使用者則回退為建立者姓名。
                EstimatorName = estimatorName,
                CreatorName = quotation.CreatedBy,
                CreatedDate = quotation.CreationTimestamp,
                ReservationDate = quotation.BookDate?.ToDateTime(TimeOnly.MinValue),
                Source = quotation.Source,
                RepairDate = quotation.FixDate?.ToDateTime(TimeOnly.MinValue)
            },
            Car = new QuotationCarInfo
            {
                CarUid = quotation.CarUid,
                LicensePlate = quotation.CarNo,
                Brand = quotation.BrandNavigation?.BrandName ?? quotation.Brand,
                Model = quotation.ModelNavigation?.ModelName ?? quotation.Model,
                Color = quotation.Color,
                Remark = quotation.CarRemark
            },
            Customer = new QuotationCustomerInfo
            {
                CustomerUid = quotation.CustomerUid,
                Name = quotation.Name,
                Phone = quotation.Phone,
                Gender = quotation.Gender,
                Source = quotation.CustomerType,
                Remark = quotation.ConnectRemark
            },
            Damages = normalizedDamages,
            CarBodyConfirmation = extraData?.CarBodyConfirmation,
            Maintenance = new QuotationMaintenanceInfo
            {
                FixTypeUid = quotation.FixTypeUid,
                FixTypeName = NormalizeOptionalText(quotation.FixTypeNavigation?.FixTypeName)
                    ?? NormalizeOptionalText(quotation.FixType),
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

        var quotation = await FindQuotationForUpdateAsync(request.QuotationUid, request.QuotationNo, cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無需更新的估價單。");
        }

        var operatorLabel = NormalizeOperator(operatorName);
        var carInfo = request.Car ?? new QuotationCarInfo();
        var customerInfo = request.Customer ?? new QuotationCustomerInfo();

        quotation.CarNo = NormalizeOptionalText(carInfo.LicensePlate)?.ToUpperInvariant() ?? quotation.CarNo;
        quotation.CarNoInput = quotation.CarNo;
        quotation.CarNoInputGlobal = quotation.CarNo;
        quotation.Brand = NormalizeOptionalText(carInfo.Brand);
        quotation.Model = NormalizeOptionalText(carInfo.Model);
        quotation.BrandModel = BuildBrandModel(quotation.Brand, quotation.Model);
        quotation.Color = NormalizeOptionalText(carInfo.Color);
        quotation.CarRemark = NormalizeOptionalText(carInfo.Remark);

        quotation.Name = NormalizeOptionalText(customerInfo.Name) ?? quotation.Name;
        quotation.Phone = NormalizeOptionalText(customerInfo.Phone);
        quotation.PhoneInput = quotation.Phone;
        quotation.PhoneInputGlobal = quotation.Phone;
        quotation.Gender = NormalizeOptionalText(customerInfo.Gender);
        quotation.CustomerType = NormalizeOptionalText(customerInfo.Source);
        quotation.ConnectRemark = NormalizeOptionalText(customerInfo.Remark);

        var (plainRemark, extraData) = ParseRemark(quotation.Remark);
        extraData ??= new QuotationExtraData();
        extraData.ServiceCategories ??= new QuotationServiceCategoryCollection();

        foreach (var kvp in request.CategoryRemarks)
        {
            var categoryKey = NormalizeCategoryKey(kvp.Key);
            if (categoryKey is null)
            {
                continue;
            }

            var block = EnsureCategoryBlock(extraData.ServiceCategories, categoryKey);
            block.Overall.Remark = NormalizeOptionalText(kvp.Value);
        }

        var newRemark = request.Remark is not null
            ? NormalizeOptionalText(request.Remark)
            : plainRemark;

        quotation.Remark = SerializeRemark(newRemark, extraData);
        quotation.ModificationTimestamp = DateTime.UtcNow;
        quotation.ModifiedBy = operatorLabel;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 更新估價單 {QuotationUid} 完成。", operatorLabel, quotation.QuotationUid);
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 針對估價單查詢套用 UID 或編號的過濾條件。
    /// </summary>
    private static IQueryable<Quatation> ApplyQuotationFilter(IQueryable<Quatation> query, string? quotationUid, string? quotationNo)
    {
        if (!string.IsNullOrWhiteSpace(quotationUid))
        {
            return query.Where(q => q.QuotationUid == quotationUid);
        }

        if (!string.IsNullOrWhiteSpace(quotationNo))
        {
            return query.Where(q => q.QuotationNo == quotationNo);
        }

        throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供估價單識別資訊。");
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
    private async Task<int> GenerateNextSerialNumberAsync(CancellationToken cancellationToken)
    {
        var maxSerial = await _context.Quatations
            .AsNoTracking()
            .MaxAsync(q => (int?)q.SerialNum, cancellationToken);

        return (maxSerial ?? 0) + 1;
    }

    /// <summary>
    /// 嘗試依據技師或門市主檔解析出門市識別碼。
    /// </summary>
    private static string? ResolveStoreUid(TechnicianEntity? technician, StoreEntity? storeEntity)
    {
        if (!string.IsNullOrWhiteSpace(technician?.StoreUid))
        {
            return NormalizeOptionalText(technician!.StoreUid);
        }

        if (!string.IsNullOrWhiteSpace(storeEntity?.StoreUid))
        {
            return NormalizeOptionalText(storeEntity!.StoreUid);
        }

        return null;
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
    /// 根據技師或門市識別碼取得門市主檔資料，確保後續可自動帶入店鋪名稱。
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
    /// 依據維修類型識別碼取得維修類型主檔，若不存在則提示呼叫端重新選擇。
    /// </summary>
    private async Task<FixTypeEntity?> GetFixTypeEntityAsync(string? fixTypeUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(fixTypeUid);
        if (normalizedUid is null)
        {
            return null;
        }

        var fixType = await _context.FixTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.FixTypeUid == normalizedUid, cancellationToken);

        if (fixType is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的維修類型，請重新選擇。");
        }

        return fixType;
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
    private async Task<Quatation?> FindQuotationForUpdateAsync(string? quotationUid, string? quotationNo, CancellationToken cancellationToken)
    {
        var query = ApplyQuotationFilter(_context.Quatations, quotationUid, quotationNo);
        return await query.FirstOrDefaultAsync(cancellationToken);
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
    /// 將備註與擴充資料序列化為 JSON 字串，統一儲存格式。
    /// </summary>
    private static string SerializeRemark(string? remark, QuotationExtraData? extra)
    {
        if (string.IsNullOrWhiteSpace(remark) && extra is null)
        {
            return string.Empty;
        }

        var envelope = new QuotationRemarkEnvelope
        {
            PlainRemark = NormalizeOptionalText(remark),
            Extra = extra
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    /// <summary>
    /// 將物件集合中的金額資訊轉換為估價單主檔的估價金額。
    /// </summary>
    private static decimal? CalculateTotalAmount(List<QuotationDamageItem> damages, decimal? otherFee, decimal? roundingDiscount, decimal? percentageDiscount)
    {
        var hasBaseAmount = false;
        decimal subtotal = 0m;

        if (damages is { Count: > 0 })
        {
            foreach (var damage in damages)
            {
                if (damage?.DisplayEstimatedAmount is not { } estimate)
                {
                    continue;
                }

                subtotal += estimate;
                hasBaseAmount = true;
            }
        }

        if (otherFee.HasValue)
        {
            subtotal += otherFee.Value;
            hasBaseAmount = true;
        }

        var result = subtotal;
        var hasDiscount = false;

        if (percentageDiscount.HasValue && percentageDiscount.Value != 0)
        {
            // 先計算折扣金額，再自總額扣除，保留原始小計供後續折扣使用。
            var discountRate = percentageDiscount.Value / 100m;
            result -= subtotal * discountRate;
            hasDiscount = true;
        }

        if (roundingDiscount.HasValue && roundingDiscount.Value != 0)
        {
            // 零頭折扣直接以金額扣除，可用來調整至整數金額。
            result -= roundingDiscount.Value;
            hasDiscount = true;
        }

        if (!hasBaseAmount && !hasDiscount)
        {
            return null;
        }

        if (result < 0)
        {
            result = 0;
        }

        return result;
    }

    /// <summary>
    /// 整理建立估價單請求內所有的照片識別碼，避免遺漏綁定。
    /// </summary>
    private static List<string> CollectPhotoUids(CreateQuotationRequest request)
    {
        var uniqueUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void TryAdd(HashSet<string> buffer, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            buffer.Add(value.Trim());
        }

        var damages = ExtractDamageList(request);
        if (damages.Count > 0)
        {
            foreach (var damage in damages)
            {
                if (damage is null)
                {
                    continue;
                }

                TryAdd(uniqueUids, damage.Photo);

                if (damage.Photos is not { Count: > 0 })
                {
                    continue;
                }

                foreach (var photo in damage.Photos)
                {
                    if (photo is null)
                    {
                        continue;
                    }

                    TryAdd(uniqueUids, photo.PhotoUid);
                    TryAdd(uniqueUids, photo.File);
                }
            }
        }

        if (request.CarBodyConfirmation is { } body)
        {
            TryAdd(uniqueUids, body.AnnotatedPhotoUid);
            TryAdd(uniqueUids, body.AnnotatedImage);
            TryAdd(uniqueUids, body.SignaturePhotoUid);
            TryAdd(uniqueUids, body.Signature);
        }

        return uniqueUids.ToList();
    }

    /// <summary>
    /// 從建立估價單請求中取得傷痕列表，僅使用新版頂層 damages 欄位。
    /// </summary>
    private static List<QuotationDamageItem> ExtractDamageList(CreateQuotationRequest request)
    {
        if (request.Damages is { Count: > 0 })
        {
            return request.Damages;
        }

        return new List<QuotationDamageItem>();
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
            "other" or "其他" => "other",
            _ => null
        };
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
    private static string? ConvertBooleanToFlag(bool? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value ? "Y" : "N";
    }

    /// <summary>
    /// 將資料庫紀錄的文字旗標轉回布林值，支援 Y/N、True/False、是/否等常見寫法。
    /// </summary>
    private static bool? ParseBooleanFlag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
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
}
