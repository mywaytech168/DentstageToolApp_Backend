using DentstageToolApp.Api.Quotations;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using CarEntity = DentstageToolApp.Infrastructure.Entities.Car;
using CustomerEntity = DentstageToolApp.Infrastructure.Entities.Customer;
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
    private readonly ILogger<QuotationService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件以供查詢使用。
    /// </summary>
    public QuotationService(DentstageToolAppContext context, ILogger<QuotationService> logger)
    {
        _context = context;
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
            // 若前端改以數字識別碼查詢，透過 FixTypeId 篩選；否則回退以文字欄位過濾。
            if (int.TryParse(query.FixType, out var fixTypeId))
            {
                quotationsQuery = quotationsQuery.Where(q => q.FixTypeId == fixTypeId);
            }
            else
            {
                quotationsQuery = quotationsQuery.Where(q => q.FixType == query.FixType);
            }
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
                on quotation.BrandId equals brand.BrandId into brandGroup
            from brand in brandGroup.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking()
                on quotation.ModelId equals model.ModelId into modelGroup
            from model in modelGroup.DefaultIfEmpty()
            join fixType in _context.FixTypes.AsNoTracking()
                on quotation.FixTypeId equals fixType.FixTypeId into fixTypeGroup
            from fixType in fixTypeGroup.DefaultIfEmpty()
            join store in _context.Stores.AsNoTracking()
                on quotation.StoreId equals store.StoreId into storeGroup
            from store in storeGroup.DefaultIfEmpty()
            join technician in _context.Technicians.AsNoTracking()
                on quotation.TechnicianId equals technician.TechnicianId into technicianGroup
            from technician in technicianGroup.DefaultIfEmpty()
            orderby quotation.CreationTimestamp ?? DateTime.MinValue descending,
                quotation.QuotationNo descending
            select new { quotation, brand, model, fixType, store, technician };

        var pagedQuery = orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(result => new QuotationSummaryResponse
            {
                QuotationNo = result.quotation.QuotationNo,
                Status = result.quotation.Status,
                CustomerName = result.quotation.Name,
                CustomerPhone = result.quotation.Phone,
                CarBrand = result.brand != null ? result.brand.BrandName : result.quotation.Brand,
                CarModel = result.model != null ? result.model.ModelName : result.quotation.Model,
                CarPlateNumber = result.quotation.CarNo,
                // 門市名稱優先採用主檔資料，若關聯不存在則回落至原欄位。
                StoreName = result.store != null ? result.store.StoreName : result.quotation.CurrentStatusUser,
                // 估價技師同樣先以主檔名稱為主。
                EstimatorName = result.technician != null ? result.technician.TechnicianName : result.quotation.UserName,
                // 建立人員暫做為製單技師資訊。
                CreatorName = result.quotation.CreatedBy,
                CreatedAt = result.quotation.CreationTimestamp,
                // 維修類型若有主檔，回傳主檔名稱，否則回退舊有欄位。
                FixType = result.fixType != null ? result.fixType.FixTypeName : result.quotation.FixType
            });

        var items = await pagedQuery.ToListAsync(cancellationToken);

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

        // 若僅提供技師識別碼，自動帶出對應門市與技師資訊，並檢查門市一致性。
        var technicianEntity = await GetTechnicianEntityAsync(storeInfo.TechnicianId, cancellationToken);
        if (technicianEntity is not null && storeInfo.StoreId.HasValue && storeInfo.StoreId.Value != technicianEntity.StoreId)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "選擇的技師與門市不一致，請重新確認。");
        }

        var storeEntity = await GetStoreEntityAsync(storeInfo, technicianEntity, cancellationToken);
        var storeName = NormalizeRequiredText(storeInfo.StoreName ?? storeEntity?.StoreName, "店鋪名稱");
        var operatorLabel = NormalizeOperator(operatorContext.OperatorName);
        var operatorUid = NormalizeOptionalText(operatorContext.UserUid);
        var estimatorName = NormalizeOptionalText(storeInfo.EstimatorName ?? technicianEntity?.TechnicianName ?? operatorLabel);
        var creatorName = NormalizeOptionalText(storeInfo.CreatorName ?? operatorLabel);
        var storeUid = NormalizeOptionalText(storeInfo.StoreUid);
        var source = NormalizeRequiredText(storeInfo.Source, "維修來源");

        // 透過車輛主檔自動帶出車牌與品牌資訊，若未選擇車輛則沿用前端輸入值。
        var carEntity = await GetCarEntityAsync(carInfo.CarUid, cancellationToken);
        var carUid = carEntity?.CarUid ?? NormalizeOptionalText(carInfo.CarUid);
        var licensePlate = NormalizeRequiredText(carInfo.LicensePlate ?? carEntity?.CarNo, "車牌號碼").ToUpperInvariant();
        var brand = NormalizeOptionalText(carInfo.Brand ?? carEntity?.Brand);
        var model = NormalizeOptionalText(carInfo.Model ?? carEntity?.Model);
        var color = NormalizeOptionalText(carInfo.Color ?? carEntity?.Color);
        var carRemark = NormalizeOptionalText(carInfo.Remark ?? carEntity?.CarRemark);

        // 透過客戶主檔自動帶出姓名與聯絡資訊，保留前端自訂覆寫的彈性。
        var customerEntity = await GetCustomerEntityAsync(customerInfo.CustomerUid, cancellationToken);
        var customerUid = customerEntity?.CustomerUid ?? NormalizeOptionalText(customerInfo.CustomerUid);
        var customerName = NormalizeRequiredText(customerInfo.Name ?? customerEntity?.Name, "客戶名稱");
        var customerPhone = NormalizeOptionalText(customerInfo.Phone ?? customerEntity?.Phone);
        var customerGender = NormalizeOptionalText(customerInfo.Gender ?? customerEntity?.Gender);
        var customerSource = NormalizeOptionalText(customerInfo.Source ?? customerEntity?.Source);
        var customerRemark = NormalizeOptionalText(customerInfo.Remark ?? customerEntity?.ConnectRemark);

        var plainRemark = NormalizeOptionalText(request.Remark);

        var createdAt = storeInfo.CreatedDate?.ToUniversalTime() ?? DateTime.UtcNow;
        var quotationDate = DateOnly.FromDateTime((storeInfo.CreatedDate ?? DateTime.UtcNow).Date);
        var reservationDate = ToDateOnly(storeInfo.ReservationDate);
        var repairDate = ToDateOnly(storeInfo.RepairDate);
        var phoneQuery = NormalizePhoneQuery(customerPhone);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 系統資料計算區 ----------
        var serialNumber = await GenerateNextSerialNumberAsync(cancellationToken);
        var quotationUid = BuildQuotationUid();
        var quotationNo = BuildQuotationNo(serialNumber, createdAt);
        var storeId = await ResolveStoreIdAsync(storeInfo, technicianEntity, storeEntity, storeName, cancellationToken);

        var extraData = new QuotationExtraData
        {
            ServiceCategories = request.ServiceCategories,
            CategoryTotal = request.CategoryTotal,
            CarBodyConfirmation = request.CarBodyConfirmation
        };

        var remarkPayload = SerializeRemark(plainRemark, extraData);
        var valuation = CalculateTotalAmount(request.CategoryTotal, request.ServiceCategories);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 實體建立區 ----------
        var quotationEntity = new Quatation
        {
            QuotationUid = quotationUid,
            QuotationNo = quotationNo,
            SerialNum = serialNumber,
            CreationTimestamp = createdAt,
            CreatedBy = creatorName ?? operatorLabel,
            ModificationTimestamp = createdAt,
            ModifiedBy = operatorLabel,
            UserUid = operatorUid,
            Date = quotationDate,
            StoreId = storeId,
            StoreUid = storeUid,
            TechnicianId = technicianEntity?.TechnicianId,
            CurrentStatusUser = storeName,
            UserName = estimatorName,
            BookDate = reservationDate,
            FixDate = repairDate,
            Source = source,
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
            Valuation = valuation
        };

        await _context.Quatations.AddAsync(quotationEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

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
            .Include(q => q.ModelNavigation);

        query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Quatation, Model?>)ApplyQuotationFilter(query, request.QuotationUid, request.QuotationNo);

        var quotation = await query.FirstOrDefaultAsync(cancellationToken);
        if (quotation is null)
        {
            throw new QuotationManagementException(HttpStatusCode.NotFound, "查無符合條件的估價單。");
        }

        var (plainRemark, extraData) = ParseRemark(quotation.Remark);

        return new QuotationDetailResponse
        {
            QuotationUid = quotation.QuotationUid,
            QuotationNo = quotation.QuotationNo,
            Status = quotation.Status,
            CreatedAt = quotation.CreationTimestamp,
            UpdatedAt = quotation.ModificationTimestamp,
            Store = new QuotationStoreInfo
            {
                StoreId = quotation.StoreId,
                StoreUid = quotation.StoreUid,
                TechnicianId = quotation.TechnicianId,
                StoreName = quotation.StoreNavigation?.StoreName ?? quotation.CurrentStatusUser ?? string.Empty,
                EstimatorName = quotation.UserName,
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
            Remark = plainRemark,
            ServiceCategories = extraData?.ServiceCategories,
            CategoryTotal = extraData?.CategoryTotal,
            CarBodyConfirmation = extraData?.CarBodyConfirmation
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
    /// 建立估價單唯一識別碼，使用 Qu_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildQuotationUid()
    {
        return $"Qu_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 依照序號與建立時間產生估價單編號。
    /// </summary>
    private static string BuildQuotationNo(int serialNumber, DateTime timestamp)
    {
        return $"Q{timestamp:yyyyMMdd}-{serialNumber:0000}";
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
    /// 嘗試依據店家資訊解析出門市識別碼，優先採用技師或已載入的門市主檔。
    /// </summary>
    private async Task<int?> ResolveStoreIdAsync(
        QuotationStoreInfo storeInfo,
        TechnicianEntity? technician,
        StoreEntity? storeEntity,
        string storeName,
        CancellationToken cancellationToken)
    {
        if (technician is not null)
        {
            return technician.StoreId;
        }

        if (storeInfo.StoreId.HasValue)
        {
            return storeInfo.StoreId;
        }

        if (storeEntity is not null)
        {
            return storeEntity.StoreId;
        }

        var normalizedName = NormalizeOptionalText(storeName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var store = await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.StoreName == normalizedName, cancellationToken);

        return store?.StoreId;
    }

    /// <summary>
    /// 依據技師識別碼載入技師與所屬門市資料，若未提供識別碼則回傳 null。
    /// </summary>
    private async Task<TechnicianEntity?> GetTechnicianEntityAsync(int? technicianId, CancellationToken cancellationToken)
    {
        if (!technicianId.HasValue)
        {
            return null;
        }

        var technician = await _context.Technicians
            .AsNoTracking()
            .Include(entity => entity.Store)
            .FirstOrDefaultAsync(entity => entity.TechnicianId == technicianId.Value, cancellationToken);

        if (technician is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的技師資料，請重新選擇技師。");
        }

        return technician;
    }

    /// <summary>
    /// 根據技師或門市識別碼取得門市主檔資料，確保後續可自動帶入店鋪名稱。
    /// </summary>
    private async Task<StoreEntity?> GetStoreEntityAsync(QuotationStoreInfo storeInfo, TechnicianEntity? technician, CancellationToken cancellationToken)
    {
        if (technician is not null)
        {
            if (technician.Store is not null)
            {
                return technician.Store;
            }

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.StoreId == technician.StoreId, cancellationToken);

            if (store is null)
            {
                throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的門市資料。");
            }

            return store;
        }

        if (storeInfo.StoreId.HasValue)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.StoreId == storeInfo.StoreId.Value, cancellationToken);

            if (store is null)
            {
                throw new QuotationManagementException(HttpStatusCode.BadRequest, "找不到對應的門市資料。");
            }

            return store;
        }

        return null;
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
    private static decimal? CalculateTotalAmount(QuotationCategoryTotal? total, QuotationServiceCategoryCollection? categories)
    {
        var hasValue = false;
        decimal amount = 0m;

        if (total?.CategorySubtotals is { Count: > 0 })
        {
            foreach (var value in total.CategorySubtotals.Values)
            {
                if (value.HasValue)
                {
                    amount += value.Value;
                    hasValue = true;
                }
            }

            if (total.RoundingDiscount.HasValue)
            {
                amount -= total.RoundingDiscount.Value;
                hasValue = true;
            }
        }
        else if (categories is not null)
        {
            foreach (var block in EnumerateCategoryBlocks(categories))
            {
                if (block.Amount.DamageSubtotal.HasValue)
                {
                    amount += block.Amount.DamageSubtotal.Value;
                    hasValue = true;
                }

                if (block.Amount.AdditionalFee.HasValue)
                {
                    amount += block.Amount.AdditionalFee.Value;
                    hasValue = true;
                }
            }
        }

        return hasValue ? amount : null;
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
    /// 將 DateTime? 轉換為 DateOnly?，方便儲存至資料庫。
    /// </summary>
    private static DateOnly? ToDateOnly(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return DateOnly.FromDateTime(value.Value.Date);
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
        public int Version { get; set; } = 1;

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
    /// 估價單擴充資料，包含服務類別與車體確認單資訊。
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
    }
}
