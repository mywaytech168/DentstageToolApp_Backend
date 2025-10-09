using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Brands;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Brand;

/// <summary>
/// 品牌查詢服務實作，提供品牌清單與單筆查詢功能。
/// </summary>
public class BrandQueryService : IBrandQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<BrandQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容與記錄器。
    /// </summary>
    public BrandQueryService(DentstageToolAppContext dbContext, ILogger<BrandQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BrandListResponse> GetBrandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = await _dbContext.Brands
                .AsNoTracking()
                .OrderBy(brand => brand.BrandName)
                .Select(brand => new BrandListItem
                {
                    BrandUid = brand.BrandUid,
                    BrandName = brand.BrandName
                })
                .ToListAsync(cancellationToken);

            return new BrandListResponse
            {
                Items = items
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("品牌列表查詢流程被取消。");
            throw new BrandQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (BrandQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢品牌列表時發生未預期錯誤。");
            throw new BrandQueryServiceException(HttpStatusCode.InternalServerError, "查詢品牌列表發生錯誤，請稍後再試。");
        }
    }

    /// <inheritdoc />
    public async Task<BrandDetailResponse> GetBrandAsync(string brandUid, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUid = (brandUid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                throw new BrandQueryServiceException(HttpStatusCode.BadRequest, "請提供品牌識別碼。");
            }

            var entity = await _dbContext.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(brand => brand.BrandUid == normalizedUid, cancellationToken);

            if (entity is null)
            {
                throw new BrandQueryServiceException(HttpStatusCode.NotFound, "找不到對應的品牌資料。");
            }

            return new BrandDetailResponse
            {
                BrandUid = entity.BrandUid,
                BrandName = entity.BrandName
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("品牌明細查詢流程被取消。");
            throw new BrandQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (BrandQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢品牌明細時發生未預期錯誤。");
            throw new BrandQueryServiceException(HttpStatusCode.InternalServerError, "查詢品牌明細發生錯誤，請稍後再試。");
        }
    }
}
