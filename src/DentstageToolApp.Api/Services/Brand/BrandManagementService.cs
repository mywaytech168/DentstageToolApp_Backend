using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Brands;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Brand;

/// <summary>
/// 品牌維運服務實作，負責處理品牌建立、更新與刪除的商業邏輯。
/// </summary>
public class BrandManagementService : IBrandManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<BrandManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public BrandManagementService(DentstageToolAppContext dbContext, ILogger<BrandManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateBrandResponse> CreateBrandAsync(CreateBrandRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new BrandManagementException(HttpStatusCode.BadRequest, "請提供品牌建立資料。");
        }

        // ---------- 參數整理區 ----------
        var brandName = NormalizeRequiredText(request.BrandName, "品牌名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var duplicate = await _dbContext.Brands
            .AsNoTracking()
            .AnyAsync(brand => brand.BrandName == brandName, cancellationToken);

        if (duplicate)
        {
            throw new BrandManagementException(HttpStatusCode.Conflict, "品牌名稱已存在，請勿重複建立。");
        }

        // ---------- 實體建立區 ----------
        var brandEntity = new Brand
        {
            BrandUid = BuildBrandUid(),
            BrandName = brandName
        };

        await _dbContext.Brands.AddAsync(brandEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增品牌 {BrandUid} ({BrandName}) 成功。", operatorLabel, brandEntity.BrandUid, brandEntity.BrandName);

        // ---------- 組裝回應區 ----------
        return new CreateBrandResponse
        {
            BrandUid = brandEntity.BrandUid,
            BrandName = brandEntity.BrandName,
            Message = "已建立品牌資料。"
        };
    }

    /// <inheritdoc />
    public async Task<EditBrandResponse> EditBrandAsync(EditBrandRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new BrandManagementException(HttpStatusCode.BadRequest, "請提供品牌編輯資料。");
        }

        // ---------- 參數整理區 ----------
        var brandUid = NormalizeRequiredText(request.BrandUid, "品牌識別碼");
        var brandName = NormalizeRequiredText(request.BrandName, "品牌名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        var brandEntity = await _dbContext.Brands
            .FirstOrDefaultAsync(brand => brand.BrandUid == brandUid, cancellationToken);

        if (brandEntity is null)
        {
            throw new BrandManagementException(HttpStatusCode.NotFound, "找不到對應的品牌資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duplicate = await _dbContext.Brands
            .AsNoTracking()
            .AnyAsync(brand => brand.BrandUid != brandUid && brand.BrandName == brandName, cancellationToken);

        if (duplicate)
        {
            throw new BrandManagementException(HttpStatusCode.Conflict, "品牌名稱已存在於其他品牌，請重新命名。");
        }

        // ---------- 實體更新區 ----------
        brandEntity.BrandName = brandName;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        _logger.LogInformation("操作人員 {Operator} 編輯品牌 {BrandUid} ({BrandName}) 成功。", operatorLabel, brandEntity.BrandUid, brandEntity.BrandName);

        // ---------- 組裝回應區 ----------
        return new EditBrandResponse
        {
            BrandUid = brandEntity.BrandUid,
            BrandName = brandEntity.BrandName,
            UpdatedAt = now,
            Message = "已更新品牌資料。"
        };
    }

    /// <inheritdoc />
    public async Task<DeleteBrandResponse> DeleteBrandAsync(DeleteBrandRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new BrandManagementException(HttpStatusCode.BadRequest, "請提供品牌刪除資料。");
        }

        // ---------- 參數整理區 ----------
        var brandUid = NormalizeRequiredText(request.BrandUid, "品牌識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        var brandEntity = await _dbContext.Brands
            .FirstOrDefaultAsync(brand => brand.BrandUid == brandUid, cancellationToken);

        if (brandEntity is null)
        {
            throw new BrandManagementException(HttpStatusCode.NotFound, "找不到對應的品牌資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var hasModels = await _dbContext.Models
            .AsNoTracking()
            .AnyAsync(model => model.BrandUid == brandUid, cancellationToken);

        if (hasModels)
        {
            throw new BrandManagementException(HttpStatusCode.Conflict, "該品牌仍有車型資料，請先刪除相關車型後再執行刪除。");
        }

        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation => quotation.BrandUid == brandUid, cancellationToken);

        if (hasQuotations)
        {
            throw new BrandManagementException(HttpStatusCode.Conflict, "該品牌仍被報價單使用，請先調整報價單資料後再刪除。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.Brands.Remove(brandEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除品牌 {BrandUid} ({BrandName}) 成功。", operatorLabel, brandEntity.BrandUid, brandEntity.BrandName);

        // ---------- 組裝回應區 ----------
        return new DeleteBrandResponse
        {
            BrandUid = brandEntity.BrandUid,
            Message = "已刪除品牌資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生品牌識別碼，統一使用 B_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildBrandUid()
    {
        return $"B_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 處理必填字串欄位，若為空則丟出例外。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BrandManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 將操作人員名稱正規化，避免出現空白字串。
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
    // 此服務目前無需實作額外生命週期邏輯，保留區塊以符合專案規範。
}
