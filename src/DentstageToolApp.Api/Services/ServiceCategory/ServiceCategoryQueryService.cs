using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Api.Models.ServiceCategories;
using DentstageToolApp.Api.Models.Pagination;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.ServiceCategory;

/// <summary>
/// 服務類別查詢服務實作，提供類別列表與明細資料。
/// </summary>
public class ServiceCategoryQueryService : IServiceCategoryQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<ServiceCategoryQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容與記錄器。
    /// </summary>
    public ServiceCategoryQueryService(DentstageToolAppContext dbContext, ILogger<ServiceCategoryQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServiceCategoryListResponse> GetServiceCategoriesAsync(PaginationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pagination = request ?? new PaginationRequest();
            var (page, pageSize) = pagination.Normalize();

            var query = _dbContext.FixTypes
                .AsNoTracking()
                .OrderBy(type => type.FixType);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(type => new ServiceCategoryListItem
                {
                    FixType = type.FixType,
                    CategoryName = type.FixTypeName
                })
                .ToListAsync(cancellationToken);

            // ---------- 陣列整理區 ----------
            // 依照系統既定的內部鍵值順序（dent、beauty、paint、other）排序，再映射成凹痕、美容、板烤或其他，避免不同資料庫排序造成顯示不一致。
            items = items
                .OrderBy(item =>
                {
                    var normalized = QuotationDamageFixTypeHelper.Normalize(item.FixType) ?? item.FixType;
                    var index = QuotationDamageFixTypeHelper.CanonicalOrder.IndexOf(normalized ?? string.Empty);
                    return index >= 0 ? index : int.MaxValue;
                })
                .ThenBy(item => item.FixType)
                .ToList();

            return new ServiceCategoryListResponse
            {
                Items = items,
                Pagination = new PaginationMetadata
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("服務類別列表查詢流程被取消。");
            throw new ServiceCategoryQueryException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (ServiceCategoryQueryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢服務類別列表時發生未預期錯誤。");
            throw new ServiceCategoryQueryException(HttpStatusCode.InternalServerError, "查詢服務類別列表發生錯誤，請稍後再試。");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceCategoryDetailResponse> GetServiceCategoryAsync(string fixType, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUid = (fixType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                throw new ServiceCategoryQueryException(HttpStatusCode.BadRequest, "請提供維修類型中文標籤。");
            }

            var canonicalFixType = QuotationDamageFixTypeHelper.Normalize(normalizedUid)
                ?? normalizedUid;

            var entity = await _dbContext.FixTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(type => type.FixType == canonicalFixType, cancellationToken);

            if (entity is null)
            {
                throw new ServiceCategoryQueryException(HttpStatusCode.NotFound, "找不到對應的服務類別資料，請確認維修類型中文標籤是否正確。");
            }

            return new ServiceCategoryDetailResponse
            {
                FixType = entity.FixType,
                CategoryName = entity.FixTypeName
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("服務類別明細查詢流程被取消。");
            throw new ServiceCategoryQueryException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (ServiceCategoryQueryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢服務類別明細時發生未預期錯誤。");
            throw new ServiceCategoryQueryException(HttpStatusCode.InternalServerError, "查詢服務類別明細發生錯誤，請稍後再試。");
        }
    }
}
