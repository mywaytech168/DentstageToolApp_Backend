using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Pagination;
using DentstageToolApp.Api.Models.Purchases;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Purchase;

/// <summary>
/// 採購模組服務實作，封裝採購單與品項類別的 CRUD 邏輯。
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<PurchaseService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public PurchaseService(DentstageToolAppContext dbContext, ILogger<PurchaseService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ---------- API 呼叫區 ----------

    /// <inheritdoc />
    public async Task<PurchaseOrderListResponse> GetPurchaseOrdersAsync(PurchaseOrderListQuery query, CancellationToken cancellationToken)
    {
        // ---------- 分頁參數整理區 ----------
        var normalizedQuery = query ?? new PurchaseOrderListQuery();
        var (page, pageSize) = normalizedQuery.Normalize();
        var skip = (page - 1) * pageSize;
        var storeKeyword = NormalizeOptionalText(normalizedQuery.StoreKeyword);
        var startDate = normalizedQuery.StartDate;
        var endDate = normalizedQuery.EndDate;

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "起始日期不可晚於結束日期。");
        }

        // ---------- 資料查詢區 ----------
        var queryable = _dbContext.PurchaseOrders.AsNoTracking();

        if (storeKeyword is not null)
        {
            // 店鋪關鍵字採用資料庫 LIKE 查詢以支援模糊比對。
            var escapedKeyword = storeKeyword
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var likePattern = $"%{escapedKeyword}%";
            queryable = queryable.Where(order => order.Store != null && order.Store.StoreName != null && EF.Functions.Like(order.Store.StoreName!, likePattern, "\\"));
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value;
            queryable = queryable.Where(order => order.PurchaseDate.HasValue && order.PurchaseDate.Value >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value;
            queryable = queryable.Where(order => order.PurchaseDate.HasValue && order.PurchaseDate.Value <= end);
        }

        var totalCount = await queryable.CountAsync(cancellationToken);

        var entities = await queryable
            .Include(order => order.Store)
            .Include(order => order.PurchaseItems)
                .ThenInclude(item => item.Category)
            .OrderByDescending(order => order.PurchaseDate)
            .ThenByDescending(order => order.CreationTimestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var response = new PurchaseOrderListResponse
        {
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            },
            Items = entities.Select(MapOrderEntity).ToList()
        };

        return response;
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDetailResponse> GetPurchaseOrderAsync(string purchaseOrderUid, CancellationToken cancellationToken)
    {
        // ---------- 參數整理區 ----------
        var normalizedUid = NormalizeRequiredText(purchaseOrderUid, "採購單識別碼");

        // ---------- 資料查詢區 ----------
        var entity = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(order => order.Store)
            .Include(order => order.PurchaseItems)
                .ThenInclude(item => item.Category)
            .FirstOrDefaultAsync(order => order.PurchaseOrderUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.NotFound, "找不到對應的採購單資料。");
        }

        return new PurchaseOrderDetailResponse
        {
            PurchaseOrderUid = entity.PurchaseOrderUid,
            PurchaseOrderNo = entity.PurchaseOrderNo,
            StoreUid = entity.StoreUid,
            StoreName = entity.Store?.StoreName,
            PurchaseDate = entity.PurchaseDate,
            TotalAmount = entity.TotalAmount,
            Items = MapOrderItems(entity.PurchaseItems)
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDetailResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "請提供採購單建立資料。");
        }

        // ---------- 參數整理區 ----------
        var storeUid = NormalizeRequiredText(request.StoreUid, "門市識別碼");
        var operatorLabel = NormalizeOperator(operatorName);
        var items = NormalizeCreateItems(request.Items);

        // ---------- 參考資料查詢區 ----------
        // 先檢查門市是否存在，避免寫入無效的關聯。
        var store = await _dbContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.StoreUid == storeUid, cancellationToken);
        if (store is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "找不到指定的門市資料。");
        }

        var categoryMap = await ResolveCategoriesAsync(items.Select(item => item.CategoryUid), cancellationToken);

        // ---------- 實體建立區 ----------
        var now = DateTime.UtcNow;
        var purchaseDate = DateOnly.FromDateTime(now);
        var orderEntity = new PurchaseOrder
        {
            PurchaseOrderUid = BuildPurchaseOrderUid(),
            PurchaseOrderNo = BuildPurchaseOrderNo(),
            PurchaseDate = purchaseDate,
            StoreUid = storeUid,
            CreatedBy = operatorLabel,
            CreationTimestamp = now
        };

        foreach (var item in items)
        {
            var categoryUid = item.CategoryUid;
            PurchaseCategory? category = null;
            if (!string.IsNullOrWhiteSpace(categoryUid))
            {
                category = categoryMap[categoryUid];
            }

            var itemEntity = new PurchaseItem
            {
                PurchaseItemUid = BuildPurchaseItemUid(),
                PurchaseOrderUid = orderEntity.PurchaseOrderUid,
                ItemName = item.ItemName!,
                CategoryUid = categoryUid,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                TotalAmount = CalculateSubtotal(item.UnitPrice, item.Quantity),
                CreatedBy = operatorLabel,
                CreationTimestamp = now,
                Category = category
            };

            orderEntity.PurchaseItems.Add(itemEntity);
        }

        orderEntity.TotalAmount = CalculateOrderTotal(orderEntity.PurchaseItems);

        // ---------- 資料儲存區 ----------
        await _dbContext.PurchaseOrders.AddAsync(orderEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增採購單 {PurchaseOrderUid} ({PurchaseOrderNo}) 成功。", operatorLabel, orderEntity.PurchaseOrderUid, orderEntity.PurchaseOrderNo);

        return new PurchaseOrderDetailResponse
        {
            PurchaseOrderUid = orderEntity.PurchaseOrderUid,
            PurchaseOrderNo = orderEntity.PurchaseOrderNo,
            StoreUid = orderEntity.StoreUid,
            StoreName = store.StoreName,
            PurchaseDate = orderEntity.PurchaseDate,
            TotalAmount = orderEntity.TotalAmount,
            Items = MapOrderItems(orderEntity.PurchaseItems)
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDetailResponse> UpdatePurchaseOrderAsync(UpdatePurchaseOrderRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "請提供採購單更新資料。");
        }

        // ---------- 參數整理區 ----------
        var normalizedUid = NormalizeRequiredText(request?.PurchaseOrderUid, "採購單識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        // ---------- 資料查詢區 ----------
        var entity = await _dbContext.PurchaseOrders
            .Include(order => order.PurchaseItems)
            .FirstOrDefaultAsync(order => order.PurchaseOrderUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.NotFound, "找不到對應的採購單資料。");
        }

        if (request.PurchaseDate.HasValue)
        {
            entity.PurchaseDate = request.PurchaseDate.Value;
        }

        if (request.StoreUid is not null)
        {
            var normalizedStoreUid = NormalizeOptionalText(request.StoreUid);
            if (normalizedStoreUid is null)
            {
                entity.StoreUid = null;
                entity.Store = null;
            }
            else
            {
                // 若帶入新的門市識別碼，需確認門市資料有效。
                var store = await _dbContext.Stores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StoreUid == normalizedStoreUid, cancellationToken);
                if (store is null)
                {
                    throw new PurchaseServiceException(HttpStatusCode.BadRequest, "找不到指定的門市資料。");
                }

                entity.StoreUid = store.StoreUid;
            }
        }

        // ---------- 參考資料查詢區 ----------
        var items = NormalizeUpdateItems(request.Items);
        var categoryMap = await ResolveCategoriesAsync(items.Select(item => item.CategoryUid), cancellationToken);
        var now = DateTime.UtcNow;

        // ---------- 品項處理區 ----------
        var existingItems = entity.PurchaseItems.ToDictionary(item => item.PurchaseItemUid, StringComparer.OrdinalIgnoreCase);
        var handledUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            PurchaseItem target;
            if (!string.IsNullOrWhiteSpace(item.PurchaseItemUid) && existingItems.TryGetValue(item.PurchaseItemUid, out var existing))
            {
                target = existing;
                handledUids.Add(existing.PurchaseItemUid);
            }
            else
            {
                target = new PurchaseItem
                {
                    PurchaseItemUid = BuildPurchaseItemUid(),
                    PurchaseOrderUid = entity.PurchaseOrderUid,
                    CreatedBy = operatorLabel,
                    CreationTimestamp = now
                };
                entity.PurchaseItems.Add(target);
                handledUids.Add(target.PurchaseItemUid);
            }

            target.ItemName = item.ItemName!;
            target.CategoryUid = item.CategoryUid;
            target.UnitPrice = item.UnitPrice;
            target.Quantity = item.Quantity;
            target.TotalAmount = CalculateSubtotal(item.UnitPrice, item.Quantity);
            target.ModifiedBy = operatorLabel;
            target.ModificationTimestamp = now;

            if (!string.IsNullOrWhiteSpace(item.CategoryUid))
            {
                target.Category = categoryMap[item.CategoryUid];
            }
            else
            {
                target.Category = null;
            }
        }

        var removedItems = entity.PurchaseItems
            .Where(item => !handledUids.Contains(item.PurchaseItemUid))
            .ToList();

        if (removedItems.Count > 0)
        {
            foreach (var removed in removedItems)
            {
                entity.PurchaseItems.Remove(removed);
                _dbContext.PurchaseItems.Remove(removed);
            }
        }

        entity.TotalAmount = CalculateOrderTotal(entity.PurchaseItems);
        entity.ModifiedBy = operatorLabel;
        entity.ModificationTimestamp = now;

        // ---------- 資料儲存區 ----------
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 更新採購單 {PurchaseOrderUid} 完成。", operatorLabel, entity.PurchaseOrderUid);

        await _dbContext.Entry(entity)
            .Collection(order => order.PurchaseItems)
            .Query()
            .Include(item => item.Category)
            .LoadAsync(cancellationToken);

        await _dbContext.Entry(entity)
            .Reference(order => order.Store)
            .LoadAsync(cancellationToken);

        return new PurchaseOrderDetailResponse
        {
            PurchaseOrderUid = entity.PurchaseOrderUid,
            PurchaseOrderNo = entity.PurchaseOrderNo,
            StoreUid = entity.StoreUid,
            StoreName = entity.Store?.StoreName,
            PurchaseDate = entity.PurchaseDate,
            TotalAmount = entity.TotalAmount,
            Items = MapOrderItems(entity.PurchaseItems)
        };
    }

    /// <inheritdoc />
    public async Task DeletePurchaseOrderAsync(DeletePurchaseOrderRequest request, string operatorName, CancellationToken cancellationToken)
    {
        // ---------- 參數整理區 ----------
        var normalizedUid = NormalizeRequiredText(request?.PurchaseOrderUid, "採購單識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        // ---------- 資料查詢區 ----------
        var entity = await _dbContext.PurchaseOrders
            .Include(order => order.PurchaseItems)
            .FirstOrDefaultAsync(order => order.PurchaseOrderUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.NotFound, "找不到對應的採購單資料。");
        }

        _dbContext.PurchaseOrders.Remove(entity);
        // ---------- 資料儲存區 ----------
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除採購單 {PurchaseOrderUid} ({PurchaseOrderNo}) 完成。", operatorLabel, entity.PurchaseOrderUid, entity.PurchaseOrderNo);
    }

    /// <inheritdoc />
    public async Task<PurchaseCategoryListResponse> GetCategoriesAsync(PurchaseCategoryListQuery query, CancellationToken cancellationToken)
    {
        // ---------- 分頁參數整理區 ----------
        var normalizedQuery = query ?? new PurchaseCategoryListQuery();
        var (page, pageSize) = normalizedQuery.Normalize();
        var skip = (page - 1) * pageSize;

        // ---------- 資料查詢區 ----------
        var queryable = _dbContext.PurchaseCategories.AsNoTracking();
        var totalCount = await queryable.CountAsync(cancellationToken);

        var categories = await queryable
            .OrderBy(category => category.CategoryName)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PurchaseCategoryListResponse
        {
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            },
            Items = categories
                .Select(category => new PurchaseCategoryDto
                {
                    CategoryUid = category.CategoryUid,
                    CategoryName = category.CategoryName
                })
                .ToList()
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseCategoryDto> CreateCategoryAsync(CreatePurchaseCategoryRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "請提供類別建立資料。");
        }

        // ---------- 參數整理區 ----------
        var categoryName = NormalizeRequiredText(request.CategoryName, "類別名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        // ---------- 重複檢查區 ----------
        var duplicate = await _dbContext.PurchaseCategories
            .AsNoTracking()
            .AnyAsync(category => category.CategoryName == categoryName, cancellationToken);

        if (duplicate)
        {
            throw new PurchaseServiceException(HttpStatusCode.Conflict, "類別名稱已存在，請勿重複建立。");
        }

        // ---------- 實體建立區 ----------
        var now = DateTime.UtcNow;
        var entity = new PurchaseCategory
        {
            CategoryUid = BuildPurchaseCategoryUid(),
            CategoryName = categoryName,
            CreatedBy = operatorLabel,
            CreationTimestamp = now
        };

        await _dbContext.PurchaseCategories.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增採購品項類別 {CategoryUid} ({CategoryName}) 成功。", operatorLabel, entity.CategoryUid, entity.CategoryName);

        return new PurchaseCategoryDto
        {
            CategoryUid = entity.CategoryUid,
            CategoryName = entity.CategoryName
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseCategoryDto> UpdateCategoryAsync(UpdatePurchaseCategoryRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "請提供類別更新資料。");
        }

        // ---------- 參數整理區 ----------
        var normalizedUid = NormalizeRequiredText(request?.CategoryUid, "類別識別碼");
        var categoryName = NormalizeRequiredText(request.CategoryName, "類別名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        // ---------- 資料查詢區 ----------
        var entity = await _dbContext.PurchaseCategories
            .FirstOrDefaultAsync(category => category.CategoryUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.NotFound, "找不到對應的採購品項類別資料。");
        }

        // ---------- 重複檢查區 ----------
        var duplicate = await _dbContext.PurchaseCategories
            .AsNoTracking()
            .AnyAsync(category => category.CategoryUid != normalizedUid && category.CategoryName == categoryName, cancellationToken);

        if (duplicate)
        {
            throw new PurchaseServiceException(HttpStatusCode.Conflict, "類別名稱已存在於其他類別，請重新命名。");
        }

        entity.CategoryName = categoryName;
        entity.ModifiedBy = operatorLabel;
        entity.ModificationTimestamp = DateTime.UtcNow;

        // ---------- 資料儲存區 ----------
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 更新採購品項類別 {CategoryUid} 完成。", operatorLabel, entity.CategoryUid);

        return new PurchaseCategoryDto
        {
            CategoryUid = entity.CategoryUid,
            CategoryName = entity.CategoryName
        };
    }

    /// <inheritdoc />
    public async Task DeleteCategoryAsync(DeletePurchaseCategoryRequest request, string operatorName, CancellationToken cancellationToken)
    {
        // ---------- 參數整理區 ----------
        var normalizedUid = NormalizeRequiredText(request?.CategoryUid, "類別識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        // ---------- 資料查詢區 ----------
        var entity = await _dbContext.PurchaseCategories
            .FirstOrDefaultAsync(category => category.CategoryUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.NotFound, "找不到對應的採購品項類別資料。");
        }

        // ---------- 參考檢核區 ----------
        var hasItems = await _dbContext.PurchaseItems
            .AsNoTracking()
            .AnyAsync(item => item.CategoryUid == normalizedUid, cancellationToken);

        if (hasItems)
        {
            throw new PurchaseServiceException(HttpStatusCode.Conflict, "仍有採購品項使用該類別，請先調整品項資料後再刪除。");
        }

        _dbContext.PurchaseCategories.Remove(entity);
        // ---------- 資料儲存區 ----------
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除採購品項類別 {CategoryUid} ({CategoryName}) 完成。", operatorLabel, entity.CategoryUid, entity.CategoryName);
    }

    // ---------- 方法區 ----------

    private static PurchaseOrderDto MapOrderEntity(PurchaseOrder entity)
    {
        return new PurchaseOrderDto
        {
            PurchaseOrderUid = entity.PurchaseOrderUid,
            PurchaseOrderNo = entity.PurchaseOrderNo,
            StoreUid = entity.StoreUid,
            StoreName = entity.Store?.StoreName,
            PurchaseDate = entity.PurchaseDate,
            TotalAmount = entity.TotalAmount,
            Items = MapOrderItems(entity.PurchaseItems)
        };
    }

    private static IReadOnlyCollection<PurchaseOrderItemDto> MapOrderItems(IEnumerable<PurchaseItem> items)
    {
        return items
            .Select(item => new PurchaseOrderItemDto
            {
                PurchaseItemUid = item.PurchaseItemUid,
                ItemName = item.ItemName,
                CategoryUid = item.CategoryUid,
                CategoryName = item.Category?.CategoryName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                TotalAmount = item.TotalAmount
            })
            .ToList();
    }

    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, $"請提供{fieldName}。");
        }

        return value.Trim();
    }

    private static string NormalizeOperator(string? operatorName)
    {
        return string.IsNullOrWhiteSpace(operatorName) ? "系統" : operatorName.Trim();
    }

    private static decimal CalculateSubtotal(decimal unitPrice, int quantity)
    {
        return Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateOrderTotal(IEnumerable<PurchaseItem> items)
    {
        var total = items.Sum(item => item.TotalAmount);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizeUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "單價不可為負數。");
        }

        return Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildPurchaseOrderUid()
    {
        return $"PO_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    private static string BuildPurchaseOrderNo()
    {
        return $"PU_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    private static string BuildPurchaseItemUid()
    {
        return $"PI_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    private static string BuildPurchaseCategoryUid()
    {
        return $"PC_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    private static List<CreatePurchaseOrderItemRequest> NormalizeCreateItems(IEnumerable<CreatePurchaseOrderItemRequest>? items)
    {
        if (items is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "請提供採購品項資料。");
        }

        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "至少需建立一筆採購品項。");
        }

        foreach (var item in list)
        {
            item.ItemName = NormalizeRequiredText(item.ItemName, "品項名稱");
            item.CategoryUid = NormalizeOptionalText(item.CategoryUid);
            item.UnitPrice = NormalizeUnitPrice(item.UnitPrice);
            if (item.Quantity <= 0)
            {
                throw new PurchaseServiceException(HttpStatusCode.BadRequest, "數量至少為 1。");
            }
        }

        return list;
    }

    private static List<UpdatePurchaseOrderItemRequest> NormalizeUpdateItems(IEnumerable<UpdatePurchaseOrderItemRequest>? items)
    {
        if (items is null)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "請提供採購品項資料。");
        }

        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new PurchaseServiceException(HttpStatusCode.BadRequest, "至少需保留一筆採購品項。");
        }

        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.PurchaseItemUid))
            {
                item.PurchaseItemUid = item.PurchaseItemUid.Trim();
            }

            item.ItemName = NormalizeRequiredText(item.ItemName, "品項名稱");
            item.CategoryUid = NormalizeOptionalText(item.CategoryUid);
            item.UnitPrice = NormalizeUnitPrice(item.UnitPrice);
            if (item.Quantity <= 0)
            {
                throw new PurchaseServiceException(HttpStatusCode.BadRequest, "數量至少為 1。");
            }
        }

        return list;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private async Task<Dictionary<string, PurchaseCategory>> ResolveCategoriesAsync(IEnumerable<string?> categoryUids, CancellationToken cancellationToken)
    {
        var uidSet = categoryUids
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Select(uid => uid!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uidSet.Count == 0)
        {
            return new Dictionary<string, PurchaseCategory>(StringComparer.OrdinalIgnoreCase);
        }

        var categories = await _dbContext.PurchaseCategories
            .Where(category => uidSet.Contains(category.CategoryUid))
            .ToListAsync(cancellationToken);

        if (categories.Count != uidSet.Count)
        {
            var missing = uidSet.Except(categories.Select(category => category.CategoryUid), StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count > 0)
            {
                throw new PurchaseServiceException(HttpStatusCode.BadRequest, $"找不到下列類別識別碼：{string.Join(", ", missing)}");
            }
        }

        return categories.ToDictionary(category => category.CategoryUid, StringComparer.OrdinalIgnoreCase);
    }
}
