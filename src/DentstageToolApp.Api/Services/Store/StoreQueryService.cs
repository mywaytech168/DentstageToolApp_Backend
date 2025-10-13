using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Stores;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市查詢服務實作，提供門市列表與明細資料。
/// </summary>
public class StoreQueryService : IStoreQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<StoreQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容與記錄器。
    /// </summary>
    public StoreQueryService(DentstageToolAppContext dbContext, ILogger<StoreQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StoreListResponse> GetStoresAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = await _dbContext.Stores
                .AsNoTracking()
                .OrderBy(store => store.StoreName)
                .Select(store => new StoreListItem
                {
                    StoreUid = store.StoreUid,
                    StoreName = store.StoreName
                })
                .ToListAsync(cancellationToken);

            return new StoreListResponse
            {
                Items = items
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("門市列表查詢流程被取消。");
            throw new StoreQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (StoreQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢門市列表時發生未預期錯誤。");
            throw new StoreQueryServiceException(HttpStatusCode.InternalServerError, "查詢門市列表發生錯誤，請稍後再試。");
        }
    }

    /// <inheritdoc />
    public async Task<StoreDetailResponse> GetStoreAsync(string storeUid, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUid = (storeUid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                throw new StoreQueryServiceException(HttpStatusCode.BadRequest, "請提供門市識別碼。");
            }

            var entity = await _dbContext.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(store => store.StoreUid == normalizedUid, cancellationToken);

            if (entity is null)
            {
                throw new StoreQueryServiceException(HttpStatusCode.NotFound, "找不到對應的門市資料。");
            }

            return new StoreDetailResponse
            {
                StoreUid = entity.StoreUid,
                StoreName = entity.StoreName
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("門市明細查詢流程被取消。");
            throw new StoreQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (StoreQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢門市明細時發生未預期錯誤。");
            throw new StoreQueryServiceException(HttpStatusCode.InternalServerError, "查詢門市明細發生錯誤，請稍後再試。");
        }
    }
}
