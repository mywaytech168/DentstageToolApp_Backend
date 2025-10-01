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
    public async Task<CreateCarResponse> CreateCarAsync(CreateCarRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CarManagementException(HttpStatusCode.BadRequest, "請提供車輛建立資料。");
        }

        // ---------- 參數整理區 ----------
        var licensePlate = (request.LicensePlateNumber ?? string.Empty).Trim();
        var normalizedPlate = NormalizePlate(licensePlate);

        if (string.IsNullOrWhiteSpace(licensePlate) || string.IsNullOrWhiteSpace(normalizedPlate))
        {
            // 若輸入全為空白或無法解析出有效車牌，直接回報錯誤避免寫入髒資料。
            throw new CarManagementException(HttpStatusCode.BadRequest, "車牌號碼格式不正確，請確認僅輸入英數字。");
        }

        var storedPlate = licensePlate.ToUpperInvariant();
        var brand = NormalizeOptionalText(request.Brand);
        var model = NormalizeOptionalText(request.Model);
        var color = NormalizeOptionalText(request.Color);
        var remark = NormalizeOptionalText(request.Remark);
        var operatorName = string.IsNullOrWhiteSpace(request.OperatorName)
            ? "CarAPI"
            : request.OperatorName!.Trim();

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
        var carEntity = new Infrastructure.Entities.Car
        {
            CarUid = Guid.NewGuid().ToString("N"),
            CarNo = storedPlate,
            CarNoQuery = normalizedPlate,
            Brand = brand,
            Model = model,
            Color = color,
            CarRemark = remark,
            BrandModel = BuildBrandModel(brand, model),
            CreationTimestamp = now,
            CreatedBy = operatorName,
            ModificationTimestamp = now,
            ModifiedBy = operatorName
        };

        await _dbContext.Cars.AddAsync(carEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 建立車輛 {CarUid} ({Plate}) 成功。", operatorName, carEntity.CarUid, storedPlate);

        // ---------- 組裝回應區 ----------
        return new CreateCarResponse
        {
            CarUid = carEntity.CarUid,
            LicensePlateNumber = carEntity.CarNo,
            Brand = carEntity.Brand,
            Model = carEntity.Model,
            Color = carEntity.Color,
            Remark = carEntity.CarRemark,
            CreatedAt = now,
            Message = "已建立車輛資料。"
        };
    }

    // ---------- 方法區 ----------

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
