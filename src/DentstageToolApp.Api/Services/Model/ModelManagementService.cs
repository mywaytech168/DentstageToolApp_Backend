using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.VehicleModels;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Model;

/// <summary>
/// 車型維運服務實作，負責處理車型建立、更新與刪除的規則。
/// </summary>
public class ModelManagementService : IModelManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<ModelManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public ModelManagementService(DentstageToolAppContext dbContext, ILogger<ModelManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateModelResponse> CreateModelAsync(CreateModelRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ModelManagementException(HttpStatusCode.BadRequest, "請提供車型建立資料。");
        }

        // ---------- 參數整理區 ----------
        var modelName = NormalizeRequiredText(request.ModelName, "車型名稱");
        var brandUid = NormalizeOptionalText(request.BrandUid);
        var operatorLabel = NormalizeOperator(operatorName);

        DentstageToolApp.Infrastructure.Entities.Brand? brandEntity = null;
        if (!string.IsNullOrEmpty(brandUid))
        {
            brandEntity = await ResolveBrandAsync(brandUid, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var duplicate = await _dbContext.Models
            .AsNoTracking()
            .AnyAsync(model =>
                model.ModelName == modelName
                && (model.BrandUid ?? string.Empty) == (brandUid ?? string.Empty),
                cancellationToken);

        if (duplicate)
        {
            throw new ModelManagementException(HttpStatusCode.Conflict, "車型名稱已存在於選定的品牌之下，請勿重複建立。");
        }

        // ---------- 實體建立區 ----------
        var modelEntity = new DentstageToolApp.Infrastructure.Entities.Model
        {
            ModelUid = BuildModelUid(),
            ModelName = modelName,
            BrandUid = brandEntity?.BrandUid
        };

        await _dbContext.Models.AddAsync(modelEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增車型 {ModelUid} ({ModelName}) 成功。", operatorLabel, modelEntity.ModelUid, modelEntity.ModelName);

        // ---------- 組裝回應區 ----------
        return new CreateModelResponse
        {
            ModelUid = modelEntity.ModelUid,
            ModelName = modelEntity.ModelName,
            BrandUid = modelEntity.BrandUid,
            Message = "已建立車型資料。"
        };
    }

    /// <inheritdoc />
    public async Task<EditModelResponse> EditModelAsync(EditModelRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ModelManagementException(HttpStatusCode.BadRequest, "請提供車型編輯資料。");
        }

        // ---------- 參數整理區 ----------
        var modelUid = NormalizeRequiredText(request.ModelUid, "車型識別碼");
        var modelName = NormalizeRequiredText(request.ModelName, "車型名稱");
        var brandUid = NormalizeOptionalText(request.BrandUid);
        var operatorLabel = NormalizeOperator(operatorName);

        var modelEntity = await _dbContext.Models
            .FirstOrDefaultAsync(model => model.ModelUid == modelUid, cancellationToken);

        if (modelEntity is null)
        {
            throw new ModelManagementException(HttpStatusCode.NotFound, "找不到對應的車型資料，請確認識別碼是否正確。");
        }

        DentstageToolApp.Infrastructure.Entities.Brand? brandEntity = null;
        if (!string.IsNullOrEmpty(brandUid))
        {
            brandEntity = await ResolveBrandAsync(brandUid, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duplicate = await _dbContext.Models
            .AsNoTracking()
            .AnyAsync(model =>
                model.ModelUid != modelUid
                && model.ModelName == modelName
                && (model.BrandUid ?? string.Empty) == (brandUid ?? string.Empty),
                cancellationToken);

        if (duplicate)
        {
            throw new ModelManagementException(HttpStatusCode.Conflict, "車型名稱已存在於其他車型中，請重新命名。");
        }

        // ---------- 實體更新區 ----------
        modelEntity.ModelName = modelName;
        modelEntity.BrandUid = brandEntity?.BrandUid;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        _logger.LogInformation("操作人員 {Operator} 編輯車型 {ModelUid} ({ModelName}) 成功。", operatorLabel, modelEntity.ModelUid, modelEntity.ModelName);

        // ---------- 組裝回應區 ----------
        return new EditModelResponse
        {
            ModelUid = modelEntity.ModelUid,
            ModelName = modelEntity.ModelName,
            BrandUid = modelEntity.BrandUid,
            UpdatedAt = now,
            Message = "已更新車型資料。"
        };
    }

    /// <inheritdoc />
    public async Task<DeleteModelResponse> DeleteModelAsync(DeleteModelRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ModelManagementException(HttpStatusCode.BadRequest, "請提供車型刪除資料。");
        }

        // ---------- 參數整理區 ----------
        var modelUid = NormalizeRequiredText(request.ModelUid, "車型識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        var modelEntity = await _dbContext.Models
            .FirstOrDefaultAsync(model => model.ModelUid == modelUid, cancellationToken);

        if (modelEntity is null)
        {
            throw new ModelManagementException(HttpStatusCode.NotFound, "找不到對應的車型資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation => quotation.ModelUid == modelUid, cancellationToken);

        if (hasQuotations)
        {
            throw new ModelManagementException(HttpStatusCode.Conflict, "該車型仍被報價單使用，請先調整報價單資料後再刪除。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.Models.Remove(modelEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除車型 {ModelUid} ({ModelName}) 成功。", operatorLabel, modelEntity.ModelUid, modelEntity.ModelName);

        // ---------- 組裝回應區 ----------
        return new DeleteModelResponse
        {
            ModelUid = modelEntity.ModelUid,
            Message = "已刪除車型資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生車型識別碼，統一使用 M_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildModelUid()
    {
        return $"M_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 處理必填字串欄位，若為空則丟出例外。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ModelManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 處理可選字串欄位，去除空白後若為空字串則回傳 null。
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
    /// 將操作人員名稱正規化。
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
    /// 依識別碼查詢品牌資料，若找不到則丟出錯誤。
    /// </summary>
    private async Task<DentstageToolApp.Infrastructure.Entities.Brand> ResolveBrandAsync(string brandUid, CancellationToken cancellationToken)
    {
        var brand = await _dbContext.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BrandUid == brandUid, cancellationToken);

        if (brand is null)
        {
            throw new ModelManagementException(HttpStatusCode.BadRequest, "找不到對應的車輛品牌，請重新選擇。");
        }

        return brand;
    }

    // ---------- 生命週期 ----------
    // 目前無額外生命週期需求，保留區塊以符合專案規範。
}
