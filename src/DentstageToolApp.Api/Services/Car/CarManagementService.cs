using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Cars;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛維運服務實作，負責處理新增車輛的資料清理與儲存流程。
/// </summary>
public class CarManagementService : ICarManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CarManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public CarManagementService(DentstageToolAppContext dbContext, ILogger<CarManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateCarResponse> CreateCarAsync(CreateCarRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "請提供車輛建立資料。");
        }

        // ---------- 參數整理區 ----------
        var licensePlate = (request.CarPlateNumber ?? string.Empty).Trim();
        var normalizedPlate = NormalizePlate(licensePlate);

        if (string.IsNullOrWhiteSpace(licensePlate) || string.IsNullOrWhiteSpace(normalizedPlate))
        {
            // 若輸入全為空白或無法解析出有效車牌，直接回報錯誤避免寫入髒資料。
            throw new CarManagementException(HttpStatusCode.BadRequest, "車牌號碼格式不正確，請確認僅輸入英數字。");
        }

        var storedPlate = licensePlate.ToUpperInvariant();
        var operatorLabel = NormalizeOperator(operatorName);
        var resolvedBrand = await ResolveBrandAsync(request.BrandUid, cancellationToken);
        var resolvedModel = await ResolveModelAsync(request.ModelUid, resolvedBrand?.BrandUid, cancellationToken);

        // 若模型提供品牌關聯而原本未帶入品牌，需補齊品牌資訊以供後續使用。
        if (!string.IsNullOrWhiteSpace(resolvedModel?.BrandUid) && resolvedBrand is null)
        {
            resolvedBrand = await ResolveBrandAsync(resolvedModel!.BrandUid, cancellationToken);
        }

        // 當品牌有傳入且模型也存在時，需確保兩者相符避免資料混亂。
        if (resolvedBrand is not null && resolvedModel is not null && !string.Equals(resolvedBrand.BrandUid, resolvedModel.BrandUid, StringComparison.Ordinal))
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "車型與品牌不相符，請重新選擇後再儲存。");
        }

        var brand = NormalizeOptionalText(resolvedBrand?.BrandName);
        var model = NormalizeOptionalText(resolvedModel?.ModelName);
        var color = NormalizeOptionalText(request.Color);
        var remark = NormalizeOptionalText(request.Remark);
        // 里程數為選填欄位，保留原始整數輸入即可，若為 null 代表現場未提供資料。
        var mileage = request.Mileage;

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var exists = await _dbContext.Cars
            .AsNoTracking()
            .AnyAsync(car =>
                car.CarNo == storedPlate
                || car.CarNo == normalizedPlate
                || car.CarNoQuery == normalizedPlate
                || car.CarNoQuery == storedPlate,
                cancellationToken);

        if (exists)
        {
            _logger.LogWarning("新增車輛失敗，車牌 {LicensePlate} 已存在。", storedPlate);
            throw new CarManagementException(HttpStatusCode.Conflict, "車牌號碼已存在，請勿重複建立。");
        }

        // ---------- 實體建立區 ----------
        var now = DateTime.UtcNow;
        var carUid = BuildCarUid();
        var carEntity = new DentstageToolApp.Infrastructure.Entities.Car
        {
            CarUid = carUid,
            CarNo = storedPlate,
            CarNoQuery = normalizedPlate,
            Brand = brand,
            Model = model,
            Color = color,
            CarRemark = remark,
            Milage = mileage,
            BrandModel = BuildBrandModel(brand, model),
            CreationTimestamp = now,
            CreatedBy = operatorLabel,
            ModificationTimestamp = now,
            ModifiedBy = operatorLabel
        };

        await _dbContext.Cars.AddAsync(carEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 建立車輛 {CarUid} ({Plate}) 成功。", operatorLabel, carEntity.CarUid, storedPlate);

        // ---------- 組裝回應區 ----------
        return new CreateCarResponse
        {
            CarUid = carEntity.CarUid,
            CarPlateNumber = carEntity.CarNo,
            BrandUid = resolvedBrand?.BrandUid,
            Brand = carEntity.Brand,
            Model = carEntity.Model,
            ModelUid = resolvedModel?.ModelUid,
            Color = carEntity.Color,
            Remark = carEntity.CarRemark,
            Mileage = carEntity.Milage,
            CreatedAt = now,
            Message = "已建立車輛資料。"
        };
    }

    /// <inheritdoc />
    public async Task<EditCarResponse> EditCarAsync(EditCarRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "請提供車輛編輯資料。");
        }

        // ---------- 參數整理區 ----------
        // 先整理識別碼避免查詢時因前後空白導致找不到資料。
        var carUid = NormalizeRequiredText(request.CarUid, "車輛識別碼");
        var licensePlate = NormalizeRequiredText(request.CarPlateNumber, "車牌號碼");
        var normalizedPlate = NormalizePlate(licensePlate);

        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            // 若清除後無有效字元，代表輸入內容不合法。
            throw new CarManagementException(HttpStatusCode.BadRequest, "車牌號碼格式不正確，請確認僅輸入英數字。");
        }

        var storedPlate = licensePlate.ToUpperInvariant();
        var operatorLabel = NormalizeOperator(operatorName);

        // 先找出欲更新的車輛，若不存在直接回報錯誤避免後續更新 null 物件。
        var carEntity = await _dbContext.Cars
            .FirstOrDefaultAsync(car => car.CarUid == carUid, cancellationToken);

        if (carEntity is null)
        {
            throw new CarManagementException(HttpStatusCode.NotFound, "找不到對應的車輛資料，請確認識別碼是否正確。");
        }

        var resolvedBrand = await ResolveBrandAsync(request.BrandUid, cancellationToken);
        var resolvedModel = await ResolveModelAsync(request.ModelUid, resolvedBrand?.BrandUid, cancellationToken);

        // 若模型帶有品牌資訊，但外部未傳入品牌，則以模型所屬品牌為主。
        if (!string.IsNullOrWhiteSpace(resolvedModel?.BrandUid) && resolvedBrand is null)
        {
            resolvedBrand = await ResolveBrandAsync(resolvedModel!.BrandUid, cancellationToken);
        }

        // 當品牌有指定且模型也有帶入時，需驗證兩者是否一致。
        if (resolvedBrand is not null && resolvedModel is not null && !string.Equals(resolvedBrand.BrandUid, resolvedModel.BrandUid, StringComparison.Ordinal))
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "車型與品牌不相符，請重新選擇後再儲存。");
        }

        var brand = NormalizeOptionalText(resolvedBrand?.BrandName);
        var model = NormalizeOptionalText(resolvedModel?.ModelName);
        var color = NormalizeOptionalText(request.Color);
        var remark = NormalizeOptionalText(request.Remark);
        // 編輯流程同樣保留里程數原始數值，避免不必要的型別轉換造成落差。
        var mileage = request.Mileage;

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        // 檢查是否有其他車輛使用相同車牌，避免資料重複。
        var duplicate = await _dbContext.Cars
            .AsNoTracking()
            .AnyAsync(car =>
                    car.CarUid != carUid
                    && (
                        car.CarNo == storedPlate
                        || car.CarNo == normalizedPlate
                        || car.CarNoQuery == normalizedPlate
                        || car.CarNoQuery == storedPlate
                    ),
                cancellationToken);

        if (duplicate)
        {
            throw new CarManagementException(HttpStatusCode.Conflict, "車牌號碼已存在於其他車輛，請重新確認。");
        }

        // ---------- 實體更新區 ----------
        var now = DateTime.UtcNow;
        carEntity.CarNo = storedPlate;
        carEntity.CarNoQuery = normalizedPlate;
        carEntity.Brand = brand;
        carEntity.Model = model;
        carEntity.Color = color;
        carEntity.CarRemark = remark;
        // 里程數允許為空值，若前端提供資料則直接覆寫以維持最新車況。
        if (mileage.HasValue)
        {
            carEntity.Milage = mileage;
        }
        carEntity.BrandModel = BuildBrandModel(brand, model);
        carEntity.ModificationTimestamp = now;
        carEntity.ModifiedBy = operatorLabel;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 編輯車輛 {CarUid} ({Plate}) 成功。", operatorLabel, carEntity.CarUid, storedPlate);

        // ---------- 組裝回應區 ----------
        return new EditCarResponse
        {
            CarUid = carEntity.CarUid,
            CarPlateNumber = carEntity.CarNo!,
            BrandUid = resolvedBrand?.BrandUid,
            Brand = carEntity.Brand,
            Model = carEntity.Model,
            ModelUid = resolvedModel?.ModelUid,
            Color = carEntity.Color,
            Remark = carEntity.CarRemark,
            Mileage = carEntity.Milage,
            UpdatedAt = now,
            Message = "已更新車輛資料。"
        };
    }

    /// <inheritdoc />
    public async Task<DeleteCarResponse> DeleteCarAsync(DeleteCarRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "請提供車輛刪除資料。");
        }

        // ---------- 參數整理區 ----------
        var carUid = NormalizeRequiredText(request.CarUid, "車輛識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        var carEntity = await _dbContext.Cars
            .FirstOrDefaultAsync(car => car.CarUid == carUid, cancellationToken);

        if (carEntity is null)
        {
            throw new CarManagementException(HttpStatusCode.NotFound, "找不到對應的車輛資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation => quotation.CarUid == carUid, cancellationToken);

        if (hasQuotations)
        {
            throw new CarManagementException(HttpStatusCode.Conflict, "該車輛仍被報價單使用，請先調整報價單資料後再刪除。");
        }

        var hasOrders = await _dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.CarUid == carUid, cancellationToken);

        if (hasOrders)
        {
            throw new CarManagementException(HttpStatusCode.Conflict, "該車輛仍有工單資料，請先處理相關工單。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.Cars.Remove(carEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除車輛 {CarUid} ({Plate}) 成功。", operatorLabel, carEntity.CarUid, carEntity.CarNo);

        // ---------- 組裝回應區 ----------
        return new DeleteCarResponse
        {
            CarUid = carEntity.CarUid,
            Message = "已刪除車輛資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生符合規範的車輛主鍵，使用 Ca_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildCarUid()
    {
        // 依照需求改為 Ca_{uuid} 格式，並轉成大寫以利閱讀。
        return $"Ca_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 將輸入車牌字串轉成僅包含英數字的大寫格式，方便搜尋與比對。
    /// </summary>
    private static string NormalizePlate(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return string.Empty;
        }

        var filtered = new string(plate.Where(char.IsLetterOrDigit).ToArray());
        return filtered.ToUpperInvariant();
    }

    /// <summary>
    /// 處理可選文字欄位，若為空則回傳 null，避免寫入多餘空白。
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
    /// 將傳入的操作人員名稱正規化，避免寫入空白或 null。
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
    /// 處理必填欄位，若為空則丟出對應的提示訊息。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 依照傳入品牌識別碼取得品牌資料，若未帶入則回傳 null。
    /// </summary>
    private async Task<DentstageToolApp.Infrastructure.Entities.Brand?> ResolveBrandAsync(string? brandUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(brandUid);
        if (normalizedUid is null)
        {
            return null;
        }

        var brand = await _dbContext.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BrandUid == normalizedUid, cancellationToken);

        if (brand is null)
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "找不到對應的車輛品牌，請重新選擇。");
        }

        return brand;
    }

    /// <summary>
    /// 依照傳入車型識別碼取得車型資料，會檢查與指定品牌是否相符。
    /// </summary>
    private async Task<DentstageToolApp.Infrastructure.Entities.Model?> ResolveModelAsync(string? modelUid, string? expectedBrandUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeOptionalText(modelUid);
        if (normalizedUid is null)
        {
            return null;
        }

        var model = await _dbContext.Models
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ModelUid == normalizedUid, cancellationToken);

        if (model is null)
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "找不到對應的車輛型號，請重新選擇。");
        }

        if (!string.IsNullOrWhiteSpace(expectedBrandUid) && !string.Equals(model.BrandUid, expectedBrandUid, StringComparison.Ordinal))
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "車型與品牌不相符，請確認選項是否正確。");
        }

        return model;
    }

    /// <summary>
    /// 建立品牌與型號的合併欄位，供後端查詢使用。
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
}
