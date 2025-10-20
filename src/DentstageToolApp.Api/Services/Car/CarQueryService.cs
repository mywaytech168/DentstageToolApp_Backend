using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Cars;
using DentstageToolApp.Api.Models.Pagination;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛查詢服務實作，負責提供車輛列表與明細資料。
/// </summary>
public class CarQueryService : ICarQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CarQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public CarQueryService(DentstageToolAppContext dbContext, ILogger<CarQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CarListResponse> GetCarsAsync(PaginationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pagination = request ?? new PaginationRequest();
            var (page, pageSize) = pagination.Normalize();

            _logger.LogDebug(
                "開始查詢車輛列表資料，頁碼：{Page}，每頁筆數：{PageSize}。",
                page,
                pageSize);

            var items = await _dbContext.Cars
                .AsNoTracking()
                .OrderByDescending(car => car.CreationTimestamp)
                .ThenBy(car => car.CarUid)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(car => new CarListItem
                {
                    CarUid = car.CarUid,
                    CarPlateNumber = car.CarNo,
                    Brand = car.Brand,
                    Model = car.Model,
                    Mileage = car.Milage,
                    CreatedAt = car.CreationTimestamp
                })
                .ToListAsync(cancellationToken);

            var totalCount = await _dbContext.Cars.CountAsync(cancellationToken);

            _logger.LogInformation(
                "車輛列表查詢完成，頁碼：{Page}，共取得 {Count} / {Total} 筆資料。",
                page,
                items.Count,
                totalCount);

            return new CarListResponse
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
            _logger.LogInformation("車輛列表查詢流程被取消。");
            throw new CarQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (CarQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢車輛列表時發生未預期錯誤。");
            throw new CarQueryServiceException(HttpStatusCode.InternalServerError, "查詢車輛列表發生錯誤，請稍後再試。");
        }
    }

    /// <inheritdoc />
    public async Task<CarDetailResponse> GetCarAsync(string carUid, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUid = (carUid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                throw new CarQueryServiceException(HttpStatusCode.BadRequest, "請提供車輛識別碼。");
            }

            _logger.LogDebug("查詢車輛明細，UID：{CarUid}。", normalizedUid);

            var entity = await _dbContext.Cars
                .AsNoTracking()
                .FirstOrDefaultAsync(car => car.CarUid == normalizedUid, cancellationToken);

            if (entity is null)
            {
                throw new CarQueryServiceException(HttpStatusCode.NotFound, "找不到對應的車輛資料。");
            }

            return new CarDetailResponse
            {
                CarUid = entity.CarUid,
                CarPlateNumber = entity.CarNo,
                Brand = entity.Brand,
                Model = entity.Model,
                Color = entity.Color,
                Remark = entity.CarRemark,
                Mileage = entity.Milage,
                CreatedAt = entity.CreationTimestamp,
                UpdatedAt = entity.ModificationTimestamp,
                CreatedBy = entity.CreatedBy,
                ModifiedBy = entity.ModifiedBy
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("車輛明細查詢流程被取消。");
            throw new CarQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (CarQueryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢車輛明細時發生未預期錯誤。");
            throw new CarQueryServiceException(HttpStatusCode.InternalServerError, "查詢車輛明細發生錯誤，請稍後再試。");
        }
    }
}
