using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Cars;
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
        var carEntity = new Infrastructure.Entities.Car
        {
            CarUid = carUid,
            CarNo = storedPlate,
            CarNoQuery = normalizedPlate,
            Brand = brand,
            Model = model,
            Color = color,
            CarRemark = remark,
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
            CreatedAt = now,
            Message = "已建立車輛資料。"
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
    /// 依照傳入品牌識別碼取得品牌資料，若未帶入則回傳 null。
    /// </summary>
    private async Task<Brand?> ResolveBrandAsync(string? brandUid, CancellationToken cancellationToken)
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
    private async Task<Model?> ResolveModelAsync(string? modelUid, string? expectedBrandUid, CancellationToken cancellationToken)
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
