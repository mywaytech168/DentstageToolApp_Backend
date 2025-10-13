using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
        var categoryName = NormalizeRequiredText(request.CategoryName, "服務類別名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var duplicate = await _dbContext.FixTypes
            .AsNoTracking()
            .AnyAsync(type => type.FixTypeName == categoryName, cancellationToken);

        if (duplicate)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "服務類別名稱已存在，請勿重複建立。");
        }

        // ---------- 實體建立區 ----------
        var entity = new FixType
        {
            FixTypeUid = BuildServiceCategoryUid(),
            FixTypeName = categoryName
        };

        await _dbContext.FixTypes.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增服務類別 {Uid} ({Name}) 成功。", operatorLabel, entity.FixTypeUid, entity.FixTypeName);

        // ---------- 組裝回應區 ----------
        return new CreateServiceCategoryResponse
        {
            ServiceCategoryUid = entity.FixTypeUid,
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
        var categoryUid = NormalizeRequiredText(request.ServiceCategoryUid, "服務類別識別碼");
        var categoryName = NormalizeRequiredText(request.CategoryName, "服務類別名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        var entity = await _dbContext.FixTypes
            .FirstOrDefaultAsync(type => type.FixTypeUid == categoryUid, cancellationToken);

        if (entity is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.NotFound, "找不到對應的服務類別資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duplicate = await _dbContext.FixTypes
            .AsNoTracking()
            .AnyAsync(type => type.FixTypeUid != categoryUid && type.FixTypeName == categoryName, cancellationToken);

        if (duplicate)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "服務類別名稱已存在於其他資料，請重新命名。");
        }

        // ---------- 實體更新區 ----------
        entity.FixTypeName = categoryName;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        _logger.LogInformation("操作人員 {Operator} 編輯服務類別 {Uid} ({Name}) 成功。", operatorLabel, entity.FixTypeUid, entity.FixTypeName);

        // ---------- 組裝回應區 ----------
        return new EditServiceCategoryResponse
        {
            ServiceCategoryUid = entity.FixTypeUid,
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
        var categoryUid = NormalizeRequiredText(request.ServiceCategoryUid, "服務類別識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        var entity = await _dbContext.FixTypes
            .FirstOrDefaultAsync(type => type.FixTypeUid == categoryUid, cancellationToken);

        if (entity is null)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.NotFound, "找不到對應的服務類別資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation => quotation.FixTypeUid == categoryUid, cancellationToken);

        if (hasQuotations)
        {
            throw new ServiceCategoryManagementException(HttpStatusCode.Conflict, "該服務類別仍被報價單使用，請先調整資料後再刪除。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.FixTypes.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除服務類別 {Uid} ({Name}) 成功。", operatorLabel, entity.FixTypeUid, entity.FixTypeName);

        // ---------- 組裝回應區 ----------
        return new DeleteServiceCategoryResponse
        {
            ServiceCategoryUid = entity.FixTypeUid,
            Message = "已刪除服務類別資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生服務類別識別碼，統一使用 Ft_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildServiceCategoryUid()
    {
        return $"Ft_{Guid.NewGuid().ToString().ToUpperInvariant()}";
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

    // ---------- 生命週期 ----------
    // 目前無額外生命週期需求，保留區塊以符合專案規範。
}
