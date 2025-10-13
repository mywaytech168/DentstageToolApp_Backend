using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Sync;

/// <summary>
/// 提供同步資料相關的核心服務實作。
/// </summary>
public class SyncService : ISyncService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<SyncService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public SyncService(DentstageToolAppContext dbContext, ILogger<SyncService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SyncUploadResult> ProcessUploadAsync(SyncUploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // ---------- 基礎防呆 ----------
        if (string.IsNullOrWhiteSpace(request.StoreId))
        {
            throw new ArgumentException("StoreId 不可為空白。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.StoreType))
        {
            throw new ArgumentException("StoreType 不可為空白。", nameof(request));
        }

        var result = new SyncUploadResult();
        var now = DateTime.UtcNow;
        var storeState = await EnsureStoreStateAsync(request.StoreId, request.StoreType, cancellationToken);

        if (request.Changes is null || request.Changes.Count == 0)
        {
            // ---------- 無異動時提早回應 ----------
            _logger.LogInformation("StoreId {StoreId} 於 {Time} 呼叫同步上傳，但無任何異動紀錄。", request.StoreId, now);
            storeState.StoreType = request.StoreType;
            storeState.LastUploadTime = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        foreach (var change in request.Changes)
        {
            if (!string.Equals(change.TableName, "orders", StringComparison.OrdinalIgnoreCase))
            {
                result.IgnoredCount++;
                continue;
            }

            var action = change.Action?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(action))
            {
                result.IgnoredCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(change.RecordId))
            {
                result.IgnoredCount++;
                continue;
            }

            if (change.Payload is null || change.Payload.Value.ValueKind == JsonValueKind.Null || change.Payload.Value.ValueKind == JsonValueKind.Undefined)
            {
                if (!string.Equals(action, "DELETE", StringComparison.Ordinal))
                {
                    result.IgnoredCount++;
                    continue;
                }
            }

            try
            {
                await ProcessOrderChangeAsync(action, change, request.StoreId, request.StoreType, now, cancellationToken);
                result.ProcessedCount++;
            }
            catch (Exception ex)
            {
                // ---------- 錯誤紀錄 ----------
                _logger.LogError(ex, "處理同步資料時發生例外，StoreId: {StoreId}, RecordId: {RecordId}", request.StoreId, change.RecordId);
                result.IgnoredCount++;
            }
        }

        storeState.StoreType = request.StoreType;
        storeState.LastUploadTime = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<SyncDownloadResponse> GetUpdatesAsync(string storeId, string storeType, DateTime? lastSyncTime, int pageSize, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeId))
        {
            throw new ArgumentException("StoreId 不可為空白。", nameof(storeId));
        }

        if (string.IsNullOrWhiteSpace(storeType))
        {
            throw new ArgumentException("StoreType 不可為空白。", nameof(storeType));
        }

        if (pageSize <= 0)
        {
            pageSize = 100;
        }

        var now = DateTime.UtcNow;
        var query = _dbContext.Orders.AsNoTracking().Where(order => order.StoreUid == storeId);

        if (lastSyncTime.HasValue)
        {
            query = query.Where(order => order.ModificationTimestamp == null || order.ModificationTimestamp > lastSyncTime.Value);
        }

        var orders = await query
            .OrderBy(order => order.ModificationTimestamp ?? order.CreationTimestamp ?? now)
            .ThenBy(order => order.OrderUid)
            .Take(pageSize)
            .Select(order => new OrderSyncDto
            {
                OrderUid = order.OrderUid,
                OrderNo = order.OrderNo,
                StoreUid = order.StoreUid,
                Amount = order.Amount,
                Status = order.Status,
                CreationTimestamp = order.CreationTimestamp,
                ModificationTimestamp = order.ModificationTimestamp,
                QuatationUid = order.QuatationUid,
                CreatedBy = order.CreatedBy,
                ModifiedBy = order.ModifiedBy
            })
            .ToListAsync(cancellationToken);

        var storeState = await EnsureStoreStateAsync(storeId, storeType, cancellationToken);
        storeState.StoreType = storeType;
        storeState.LastDownloadTime = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SyncDownloadResponse
        {
            StoreId = storeId,
            StoreType = storeType,
            ServerTime = now,
            Orders = orders
        };
    }

    /// <summary>
    /// 處理工單資料的同步行為。
    /// </summary>
    private async Task ProcessOrderChangeAsync(string action, SyncChangeDto change, string storeId, string storeType, DateTime processTime, CancellationToken cancellationToken)
    {
        OrderSyncDto? orderDto = null;
        if (!string.Equals(action, "DELETE", StringComparison.Ordinal))
        {
            orderDto = change.Payload is null
                ? null
                : JsonSerializer.Deserialize<OrderSyncDto>(change.Payload.Value.GetRawText(), SerializerOptions);

            if (orderDto is null)
            {
                throw new InvalidOperationException("無法解析工單同步內容。");
            }
        }

        switch (action)
        {
            case "INSERT":
            case "UPDATE":
                if (orderDto is null)
                {
                    throw new InvalidOperationException("INSERT/UPDATE 必須提供工單內容。");
                }

                var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderUid == orderDto.OrderUid, cancellationToken);
                if (order is null)
                {
                    order = new Order
                    {
                        OrderUid = orderDto.OrderUid,
                        CreationTimestamp = orderDto.CreationTimestamp ?? change.UpdatedAt ?? processTime,
                        CreatedBy = orderDto.CreatedBy ?? storeId
                    };
                    await _dbContext.Orders.AddAsync(order, cancellationToken);
                }

                ApplyOrderChanges(order, orderDto, change, storeId, processTime);
                break;
            case "DELETE":
                var existedOrder = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderUid == change.RecordId, cancellationToken);
                if (existedOrder is not null)
                {
                    _dbContext.Orders.Remove(existedOrder);
                }
                break;
            default:
                throw new InvalidOperationException($"不支援的同步動作：{action}");
        }

        _dbContext.SyncLogs.Add(new SyncLog
        {
            TableName = "Orders",
            RecordId = orderDto?.OrderUid ?? change.RecordId,
            Action = action,
            UpdatedAt = change.UpdatedAt ?? orderDto?.ModificationTimestamp ?? processTime,
            SourceServer = storeId,
            StoreType = storeType,
            Synced = true
        });
    }

    /// <summary>
    /// 依據同步資料更新工單欄位。
    /// </summary>
    private static void ApplyOrderChanges(Order order, OrderSyncDto dto, SyncChangeDto change, string storeId, DateTime processTime)
    {
        order.StoreUid = dto.StoreUid ?? order.StoreUid ?? storeId;
        order.OrderNo = dto.OrderNo ?? order.OrderNo;
        order.Status = dto.Status ?? order.Status;
        order.Amount = dto.Amount ?? order.Amount;
        order.QuatationUid = dto.QuatationUid ?? order.QuatationUid;
        order.CreatedBy = dto.CreatedBy ?? order.CreatedBy ?? storeId;
        order.ModifiedBy = dto.ModifiedBy ?? storeId;
        order.ModificationTimestamp = dto.ModificationTimestamp ?? change.UpdatedAt ?? processTime;

        if (order.CreationTimestamp is null)
        {
            order.CreationTimestamp = dto.CreationTimestamp ?? change.UpdatedAt ?? processTime;
        }
    }

    /// <summary>
    /// 確保門市同步狀態存在，若無則依門市型態建立一筆獨立資料。
    /// </summary>
    private async Task<StoreSyncState> EnsureStoreStateAsync(string storeId, string storeType, CancellationToken cancellationToken)
    {
        var storeState = await _dbContext.StoreSyncStates.FirstOrDefaultAsync(x => x.StoreId == storeId && x.StoreType == storeType, cancellationToken);
        if (storeState is not null)
        {
            return storeState;
        }

        storeState = new StoreSyncState
        {
            StoreId = storeId,
            StoreType = storeType,
            LastUploadTime = null,
            LastDownloadTime = null
        };
        await _dbContext.StoreSyncStates.AddAsync(storeState, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return storeState;
    }
}
