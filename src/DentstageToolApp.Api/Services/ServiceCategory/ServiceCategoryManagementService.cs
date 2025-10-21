using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Models.ServiceCategories;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.ServiceCategory;

/// <summary>
/// 服務類別維運服務實作，負責處理維修類型主檔的 CRUD 邏輯。
/// </summary>
public class ServiceCategoryManagementService : IServiceCategoryManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<ServiceCategoryManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public ServiceCategoryManagementService(DentstageToolAppContext dbContext, ILogger<ServiceCategoryManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateServiceCategoryResponse> CreateServiceCategoryAsync(CreateServiceCategoryRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, "請提供服務類別建立資料。");
        }

        // ---------- 參數整理區 ----------
        var fixTypeRaw = NormalizeRequiredText(request.FixType, "維修類型中文標籤");
        var categoryName = NormalizeRequiredText(request.CategoryName, "服務類別名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        var canonicalFixType = QuotationDamageFixTypeHelper.Normalize(fixTypeRaw);
        if (canonicalFixType is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, "維修類型僅支援凹痕、美容、板烤或其他等固定選項。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var duplicate = await _dbContext.FixTypes
            .AsNoTracking()
            .AnyAsync(type => type.FixType == canonicalFixType, cancellationToken);

        if (duplicate)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "維修類型已存在，請勿重複建立。");
        }

        // 若名稱已被其他類別使用則拒絕建立，避免造成顯示時的混淆。
        var displayDuplicate = await _dbContext.FixTypes
            .AsNoTracking()
            .AnyAsync(type => type.FixTypeName == categoryName, cancellationToken);

        if (displayDuplicate)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "服務類別名稱已被其他維修類型使用，請重新命名。");
        }

        // ---------- 實體建立區 ----------
        var entity = new FixType
        {
            FixType = canonicalFixType,
            FixTypeName = categoryName
        };

        await _dbContext.FixTypes.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增服務類別 {FixType} ({Name}) 成功。", operatorLabel, entity.FixType, entity.FixTypeName);

        // ---------- 組裝回應區 ----------
        return new CreateServiceCategoryResponse
        {
            FixType = entity.FixType,
            CategoryName = entity.FixTypeName,
            Message = "已建立服務類別資料。"
        };
    }

    /// <inheritdoc />
    public async Task<EditServiceCategoryResponse> EditServiceCategoryAsync(EditServiceCategoryRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, "請提供服務類別編輯資料。");
        }

        // ---------- 參數整理區 ----------
        var fixTypeRaw = NormalizeRequiredText(request.FixType, "維修類型中文標籤");
        var categoryName = NormalizeRequiredText(request.CategoryName, "服務類別名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        var canonicalFixType = QuotationDamageFixTypeHelper.Normalize(fixTypeRaw);
        if (canonicalFixType is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, "維修類型僅支援凹痕、美容、板烤或其他等固定選項。");
        }

        var entity = await _dbContext.FixTypes
            .FirstOrDefaultAsync(type => type.FixType == canonicalFixType, cancellationToken);

        if (entity is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.NotFound, "找不到對應的服務類別資料，請確認維修類型中文標籤是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duplicate = await _dbContext.FixTypes
            .AsNoTracking()
            .AnyAsync(type => type.FixType != canonicalFixType && type.FixTypeName == categoryName, cancellationToken);

        if (duplicate)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "服務類別名稱已存在於其他資料，請重新命名。");
        }

        // ---------- 實體更新區 ----------
        entity.FixTypeName = categoryName;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        _logger.LogInformation("操作人員 {Operator} 編輯服務類別 {FixType} ({Name}) 成功。", operatorLabel, entity.FixType, entity.FixTypeName);

        // ---------- 組裝回應區 ----------
        return new EditServiceCategoryResponse
        {
            FixType = entity.FixType,
            CategoryName = entity.FixTypeName,
            UpdatedAt = now,
            Message = "已更新服務類別資料。"
        };
    }

    /// <inheritdoc />
    public async Task<DeleteServiceCategoryResponse> DeleteServiceCategoryAsync(DeleteServiceCategoryRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, "請提供服務類別刪除資料。");
        }

        // ---------- 參數整理區 ----------
        var fixTypeRaw = NormalizeRequiredText(request.FixType, "維修類型中文標籤");
        var operatorLabel = NormalizeOperator(operatorName);

        var canonicalFixType = QuotationDamageFixTypeHelper.Normalize(fixTypeRaw);
        if (canonicalFixType is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, "維修類型僅支援凹痕、美容、板烤或其他等固定選項。");
        }

        var entity = await _dbContext.FixTypes
            .FirstOrDefaultAsync(type => type.FixType == canonicalFixType, cancellationToken);

        if (entity is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.NotFound, "找不到對應的服務類別資料，請確認維修類型中文標籤是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var normalizedCategoryKey = QuotationDamageFixTypeHelper.Normalize(entity.FixTypeName)
            ?? QuotationDamageFixTypeHelper.Normalize(entity.FixType);
        var displayName = normalizedCategoryKey is null
            ? NormalizeOptionalText(entity.FixTypeName)
            : QuotationDamageFixTypeHelper.ResolveDisplayName(normalizedCategoryKey);
        var categoryCandidates = new[]
            {
                NormalizeOptionalText(entity.FixTypeName),
                NormalizeOptionalText(entity.FixType),
                normalizedCategoryKey,
                displayName
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => value.ToUpperInvariant())
            .ToList();

        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation =>
                categoryCandidates.Contains((quotation.FixType ?? string.Empty).ToUpper()), cancellationToken);

        if (hasQuotations)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "該服務類別仍被報價單使用，請先調整資料後再刪除。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.FixTypes.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除服務類別 {FixType} ({Name}) 成功。", operatorLabel, entity.FixType, entity.FixTypeName);

        // ---------- 組裝回應區 ----------
        return new DeleteServiceCategoryResponse
        {
            FixType = entity.FixType,
            Message = "已刪除服務類別資料。"
        };
    }

    /// <summary>
    /// 處理必填字串欄位，若為空則丟出例外。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
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
    /// 將選填欄位進行修剪，若為空字串則回傳 null。
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    // ---------- 生命週期 ----------
    // 目前無額外生命週期需求，保留區塊以符合專案規範。
}
