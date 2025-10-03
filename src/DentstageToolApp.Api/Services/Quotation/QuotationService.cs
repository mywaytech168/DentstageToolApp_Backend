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
using System.Globalization;
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
    // 序號計算時僅需少量資料即可取得最大值，限制撈取數量降低資料庫負擔。
    private const int SerialCandidateFetchCount = 50;
    private static readonly string[] TaipeiTimeZoneIds = { "Taipei Standard Time", "Asia/Taipei" };

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
        var creatorName = operatorLabel;
        var source = NormalizeRequiredText(storeInfo.Source, "維修來源");

        // ---------- 維修設定處理 ----------
        // 依據維修類型 UID 驗證主檔，並轉換常見布林選項為資料表欄位使用的旗標文字。
        var maintenanceInfo = request.Maintenance ?? new CreateQuotationMaintenanceInfo();
        var fixTypeUid = NormalizeRequiredText(maintenanceInfo.FixTypeUid, "維修類型");
        var fixTypeEntity = await GetFixTypeEntityAsync(fixTypeUid, cancellationToken);
        var fixTypeName = NormalizeOptionalText(fixTypeEntity?.FixTypeName) ?? fixTypeUid;
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

        var normalizedDamages = ExtractDamageList(request);
        var carBodyConfirmation = request.CarBodyConfirmation;

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
            unrepairableReason);
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
            UserUid = estimatorUid,
            Date = quotationDate,
            StoreUid = storeUid,
            TechnicianUid = estimatorUid,
            Status = "110",
            Status110Timestamp = createdAt,
            Status110User = storeName,
            CurrentStatusDate = createdAt,
            CurrentStatusUser = storeName,
            UserName = estimatorName,
            BookDate = reservationDate,
            FixDate = repairDate,
            FixTypeUid = fixTypeUid,
            FixType = fixTypeName,
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
            // 消息來源優先採用門市輸入，若門市未提供則以客戶主檔資料補齊。
            Source = source ?? customerSource,
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

        await _context.Quatations.AddAsync(quotationEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // 將建立流程中帶入的所有 PhotoUID 一次綁定到新建立的估價單。
        var photoUids = CollectPhotoUids(normalizedDamages, carBodyConfirmation);
        if (photoUids.Count > 0)
        {
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
            .Include(q => q.ModelNavigation)
            .Include(q => q.FixTypeNavigation);

        // 估價單編號過濾邏輯與 Include 不衝突，因此直接回寫 IQueryable 以避免轉型例外。
        query = ApplyQuotationFilter(query, null, quotationNo);

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
        normalizedDamages = await NormalizeDamagesWithPhotoDataAsync(
            quotation.QuotationUid,
            normalizedDamages,
            extraData?.CarBodyConfirmation?.SignaturePhotoUid,
            cancellationToken);
        // 依前端需求重新整理傷痕資料，只保留核心欄位並挑選主要照片。
        var simplifiedDamages = BuildDamageSummaries(normalizedDamages);
        // 移除多餘欄位的車體確認單內容，僅保留必要資訊。
        var simplifiedCarBody = SimplifyCarBodyConfirmation(extraData?.CarBodyConfirmation);
        var maintenanceRemark = plainRemark;
        var otherFee = extraData?.OtherFee;
        var roundingDiscount = extraData?.RoundingDiscount ?? quotation.Discount;
        var percentageDiscount = extraData?.PercentageDiscount ?? quotation.DiscountPercent;
        var discountReason = NormalizeOptionalText(extraData?.DiscountReason ?? quotation.DiscountReason);
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
                // 估價技師識別碼需與建立估價單時相同，方便前端直接帶入技師選項。
                TechnicianUid = NormalizeOptionalText(quotation.TechnicianUid),
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
                BrandUid = quotation.BrandUid,
                ModelUid = quotation.ModelUid,
                Color = quotation.Color,
                Remark = quotation.CarRemark
            },
            Customer = new QuotationCustomerInfo
            {
                CustomerUid = quotation.CustomerUid,
                Name = quotation.Name,
                Phone = quotation.Phone,
                Gender = quotation.Gender,
                CustomerType = quotation.CustomerType,
                County = quotation.County,
                Township = quotation.Township,
                Reason = quotation.Reason,
                Source = quotation.Source,
                Remark = quotation.ConnectRemark
            },
            Damages = simplifiedDamages,
            CarBodyConfirmation = simplifiedCarBody,
            Maintenance = new QuotationMaintenanceInfo
            {
                FixTypeUid = quotation.FixTypeUid,
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
        var requestedDamages = request.Damages ?? new List<QuotationDamageItem>();
        var (plainRemark, existingExtra) = ParseRemark(quotation.Remark);
        var maintenanceInfo = request.Maintenance;

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

        // ---------- 客戶資料同步 ----------
        quotation.Name = NormalizeOptionalText(customerInfo.Name) ?? quotation.Name;

        if (customerInfo.Phone is not null)
        {
            quotation.Phone = NormalizeOptionalText(customerInfo.Phone);
            quotation.PhoneInput = quotation.Phone;
            quotation.PhoneInputGlobal = quotation.Phone;
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

        quotation.ConnectRemark = NormalizeOptionalText(customerInfo.Remark);

        // ---------- 傷痕、簽名與維修資訊同步 ----------
        var effectiveDamages = requestedDamages.Count > 0
            ? requestedDamages
            : existingExtra?.Damages ?? new List<QuotationDamageItem>();
        var carBodyConfirmation = request.CarBodyConfirmation ?? existingExtra?.CarBodyConfirmation;

        var maintenanceRemark = plainRemark;
        decimal? otherFee = existingExtra?.OtherFee;
        decimal? roundingDiscount = quotation.Discount ?? existingExtra?.RoundingDiscount;
        decimal? percentageDiscount = quotation.DiscountPercent ?? existingExtra?.PercentageDiscount;
        var discountReason = NormalizeOptionalText(existingExtra?.DiscountReason ?? quotation.DiscountReason);
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

            if (!string.IsNullOrWhiteSpace(maintenanceInfo.FixTypeUid) &&
                !string.Equals(maintenanceInfo.FixTypeUid, quotation.FixTypeUid, StringComparison.OrdinalIgnoreCase))
            {
                var fixTypeEntity = await GetFixTypeEntityAsync(maintenanceInfo.FixTypeUid, cancellationToken);
                quotation.FixTypeUid = NormalizeOptionalText(fixTypeEntity?.FixTypeUid) ?? NormalizeOptionalText(maintenanceInfo.FixTypeUid);
                quotation.FixType = NormalizeOptionalText(fixTypeEntity?.FixTypeName)
                    ?? NormalizeOptionalText(maintenanceInfo.FixTypeUid);
            }

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

            if (maintenanceInfo.OtherFee.HasValue)
            {
                otherFee = maintenanceInfo.OtherFee;
            }

            if (maintenanceInfo.RoundingDiscount.HasValue)
            {
                roundingDiscount = maintenanceInfo.RoundingDiscount;
            }

            if (maintenanceInfo.PercentageDiscount.HasValue)
            {
                percentageDiscount = maintenanceInfo.PercentageDiscount;
            }

            if (maintenanceInfo.DiscountReason is not null)
            {
                discountReason = NormalizeOptionalText(maintenanceInfo.DiscountReason);
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

        quotation.Discount = roundingDiscount;
        quotation.DiscountPercent = percentageDiscount;
        quotation.DiscountReason = discountReason;
        quotation.FixTimeHour = fixTimeHour;
        quotation.FixTimeMin = fixTimeMin;
        quotation.FixExpectDay = fixExpectDay;
        quotation.FixExpectHour = fixExpectHour;
        quotation.FixExpect = FormatEstimatedRestorationPercentage(estimatedRestorationPercentage);

        var rejectFlag = !string.IsNullOrEmpty(unrepairableReason);
        quotation.Reject = rejectFlag ? true : null;
        quotation.RejectReason = rejectFlag ? unrepairableReason : null;

        var panelBeatFlag = !string.IsNullOrEmpty(suggestedPaintReason);
        quotation.PanelBeat = panelBeatFlag ? "1" : null;
        quotation.PanelBeatReason = panelBeatFlag ? suggestedPaintReason : null;

        var valuation = CalculateTotalAmount(effectiveDamages, otherFee, roundingDiscount, percentageDiscount);
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
            unrepairableReason);
        quotation.Remark = SerializeRemark(maintenanceRemark, extraData);
        quotation.ModificationTimestamp = DateTime.UtcNow;
        quotation.ModifiedBy = operatorLabel;

        await _context.SaveChangesAsync(cancellationToken);

        var photoUids = CollectPhotoUids(effectiveDamages, carBodyConfirmation);
        if (photoUids.Count > 0)
        {
            await _photoService.BindToQuotationAsync(quotation.QuotationUid, photoUids, cancellationToken);
        }

        await SyncDamagePhotoMetadataAsync(effectiveDamages, carBodyConfirmation?.SignaturePhotoUid, cancellationToken);
        await MarkSignaturePhotoAsync(carBodyConfirmation?.SignaturePhotoUid, cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 更新估價單 {QuotationUid} 完成。", operatorLabel, quotation.QuotationUid);
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 針對估價單查詢套用 UID 或編號的過濾條件。
    /// </summary>
    private static IQueryable<Quatation> ApplyQuotationFilter(IQueryable<Quatation> query, string? quotationUid, string? quotationNo)
    {
        // 明細查詢改以估價單編號為主，因此先以編號比對；若舊端仍傳入 UID 則保留向後相容。
        if (!string.IsNullOrWhiteSpace(quotationNo))
        {
            return query.Where(q => q.QuotationNo == quotationNo);
        }

        if (!string.IsNullOrWhiteSpace(quotationUid))
        {
            return query.Where(q => q.QuotationUid == quotationUid);
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
    /// 估價單序號候選資料結構，封裝序號欄位與估價單編號，便於後續解析。
    /// </summary>
    private sealed record SerialCandidate(int? SerialNum, string? QuotationNo);

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

            // 再從 QuotationNo 補捉舊資料留下的序號數字，避免序號回到 0001。
            if (candidate.QuotationNo is string quotationNo)
            {
                var parsedSerial = TryParseSerialFromQuotationNo(quotationNo, prefix);
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
    private static int? TryParseSerialFromQuotationNo(string? quotationNo, string prefix)
    {
        if (string.IsNullOrWhiteSpace(quotationNo))
        {
            return null;
        }

        var trimmed = quotationNo.Trim();
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
        string? unrepairableReason)
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
            || !string.IsNullOrWhiteSpace(unrepairableReason);

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
            UnrepairableReason = unrepairableReason
        };
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

        var metadata = new List<(string PhotoUid, string? Position, string? DentStatus, string? Description, decimal? Amount)>();
        var normalizedSignatureUid = NormalizeOptionalText(signaturePhotoUid);

        foreach (var damage in damages)
        {
            if (damage is null)
            {
                continue;
            }

            var position = NormalizeOptionalText(damage.DisplayPosition);
            var dentStatus = NormalizeOptionalText(damage.DisplayDentStatus);
            var description = NormalizeOptionalText(damage.DisplayDescription);
            var amount = damage.DisplayEstimatedAmount;

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

                metadata.Add((photoUid, position, dentStatus, description, amount));
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

        if (updated)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 依據照片主檔補齊傷痕欄位，兼容尚未寫入 remark JSON 的舊資料。
    /// </summary>
    private async Task<List<QuotationDamageItem>> NormalizeDamagesWithPhotoDataAsync(
        string? quotationUid,
        List<QuotationDamageItem> damages,
        string? signaturePhotoUid,
        CancellationToken cancellationToken)
    {
        var normalizedQuotationUid = NormalizeOptionalText(quotationUid);
        if (normalizedQuotationUid is null)
        {
            return damages;
        }

        var photos = await _context.PhotoData
            .AsNoTracking()
            .Where(photo => photo.QuotationUid == normalizedQuotationUid)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return damages;
        }

        var filteredPhotos = photos
            .Where(photo => string.IsNullOrWhiteSpace(signaturePhotoUid)
                || !string.Equals(photo.PhotoUid, signaturePhotoUid, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (damages.Count == 0)
        {
            return BuildDamagesFromPhotoData(filteredPhotos);
        }

        EnrichDamagesFromPhotoData(damages, filteredPhotos);
        return damages;
    }

    /// <summary>
    /// 由照片資料建立傷痕清單，供舊資料回傳使用。
    /// </summary>
    private static List<QuotationDamageItem> BuildDamagesFromPhotoData(IEnumerable<PhotoDatum> photos)
    {
        var result = new List<QuotationDamageItem>();

        foreach (var photo in photos)
        {
            var photoUid = NormalizeOptionalText(photo?.PhotoUid);
            if (photoUid is null)
            {
                continue;
            }

            var damage = new QuotationDamageItem
            {
                DisplayPhotos = new List<QuotationDamagePhoto>
                {
                    new()
                    {
                        PhotoUid = photoUid,
                        Description = photo?.Comment,
                        IsPrimary = true
                    }
                },
                DisplayPosition = photo?.Posion,
                DisplayDentStatus = photo?.PhotoShapeShow,
                DisplayDescription = photo?.Comment,
                DisplayEstimatedAmount = photo?.Cost
            };

            result.Add(damage);
        }

        return result;
    }

    /// <summary>
    /// 使用照片主檔補齊現有傷痕欄位，避免資料遺失。
    /// </summary>
    private static void EnrichDamagesFromPhotoData(List<QuotationDamageItem> damages, IReadOnlyCollection<PhotoDatum> photos)
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

                if (damage.Photos is { Count: > 0 })
                {
                    foreach (var photoInfo in damage.Photos)
                    {
                        if (photoInfo is null)
                        {
                            continue;
                        }

                        var normalizedUid = NormalizeOptionalText(photoInfo.PhotoUid) ?? NormalizeOptionalText(photoInfo.File);
                        if (normalizedUid is null || !string.Equals(normalizedUid, photo.PhotoUid, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(photoInfo.Description) && !string.IsNullOrWhiteSpace(photo.Comment))
                        {
                            photoInfo.Description = photo.Comment;
                        }
                    }
                }
            }
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

        if (damage.Photos is not { Count: > 0 })
        {
            yield break;
        }

        foreach (var photo in damage.Photos)
        {
            if (photo is null)
            {
                continue;
            }

            var uid = NormalizeOptionalText(photo.PhotoUid) ?? NormalizeOptionalText(photo.File);
            if (uid is not null)
            {
                yield return uid;
            }
        }
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

            var primaryPhotoUid = NormalizeOptionalText(ExtractPrimaryPhotoUid(damage));
            summaries.Add(new QuotationDamageSummary
            {
                Photos = primaryPhotoUid,
                Position = NormalizeOptionalText(damage.Position),
                DentStatus = NormalizeOptionalText(damage.DentStatus),
                Description = NormalizeOptionalText(damage.Description),
                EstimatedAmount = damage.EstimatedAmount
            });
        }

        return summaries;
    }

    /// <summary>
    /// 依主要照片標記決定輸出照片識別碼，若無標記則使用第一筆資料。
    /// </summary>
    private static string? ExtractPrimaryPhotoUid(QuotationDamageItem damage)
    {
        if (damage.Photos is { Count: > 0 })
        {
            var primary = damage.Photos
                .FirstOrDefault(photo => photo?.IsPrimary == true && !string.IsNullOrWhiteSpace(photo.PhotoUid));
            if (primary?.PhotoUid is { } primaryUid && !string.IsNullOrWhiteSpace(primaryUid))
            {
                return primaryUid;
            }

            var fallback = damage.Photos
                .FirstOrDefault(photo => !string.IsNullOrWhiteSpace(photo?.PhotoUid));
            if (fallback?.PhotoUid is { } fallbackUid && !string.IsNullOrWhiteSpace(fallbackUid))
            {
                return fallbackUid;
            }

            var legacy = damage.Photos
                .FirstOrDefault(photo => !string.IsNullOrWhiteSpace(photo?.File));
            if (legacy?.File is { } legacyFile && !string.IsNullOrWhiteSpace(legacyFile))
            {
                return legacyFile;
            }
        }

        return damage.Photo;
    }

    /// <summary>
    /// 精簡車體確認單資料，移除標註圖片與簽名字串欄位。
    /// </summary>
    private static QuotationCarBodyConfirmation? SimplifyCarBodyConfirmation(QuotationCarBodyConfirmation? source)
    {
        if (source is null)
        {
            return null;
        }

        var markers = source.DamageMarkers is { Count: > 0 }
            ? source.DamageMarkers
                .Select(marker => new QuotationCarBodyDamageMarker
                {
                    X = marker.X,
                    Y = marker.Y,
                    HasDent = marker.HasDent,
                    HasScratch = marker.HasScratch,
                    HasPaintPeel = marker.HasPaintPeel,
                    Remark = marker.Remark
                })
                .ToList()
            : new List<QuotationCarBodyDamageMarker>();

        return new QuotationCarBodyConfirmation
        {
            DamageMarkers = markers,
            SignaturePhotoUid = NormalizeOptionalText(source.SignaturePhotoUid)
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
