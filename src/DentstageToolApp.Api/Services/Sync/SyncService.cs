using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
    public async Task<SyncUploadResult> ProcessUploadAsync(SyncUploadRequest request, string? remoteIpAddress, CancellationToken cancellationToken)
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
        var normalizedRole = SyncServerRoles.Normalize(request.ServerRole ?? request.StoreType);
        var resolvedIp = string.IsNullOrWhiteSpace(remoteIpAddress) ? request.ServerIp : remoteIpAddress;
        var storeState = await EnsureStoreStateAsync(request.StoreId, request.StoreType, normalizedRole, resolvedIp, cancellationToken);

        if (request.Changes is null || request.Changes.Count == 0)
        {
            // ---------- 無異動時提早回應 ----------
            _logger.LogInformation("StoreId {StoreId} 於 {Time} 呼叫同步上傳，但無任何異動紀錄。", request.StoreId, now);
            storeState.StoreType = request.StoreType;
            storeState.ServerRole = normalizedRole;
            if (!string.IsNullOrWhiteSpace(resolvedIp))
            {
                storeState.ServerIp = resolvedIp;
            }
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
        storeState.ServerRole = normalizedRole;
        if (!string.IsNullOrWhiteSpace(resolvedIp))
        {
            storeState.ServerIp = resolvedIp;
        }
        storeState.LastUploadTime = now;
        _dbContext.SetSyncLogMetadata(request.StoreId, request.StoreType);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<SyncDownloadResponse> GetUpdatesAsync(string storeId, string storeType, DateTime? lastSyncTime, int pageSize, string? remoteServerRole, string? remoteIpAddress, CancellationToken cancellationToken)
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

        var serverTime = DateTime.UtcNow;

        // ---------- 依同步紀錄判斷需要下發的異動 ----------
        var logsQuery = _dbContext.SyncLogs
            .AsNoTracking()
            .Where(log => string.IsNullOrWhiteSpace(log.StoreType) || string.Equals(log.StoreType, storeType, StringComparison.OrdinalIgnoreCase));

        if (lastSyncTime.HasValue)
        {
            // ---------- 僅取回上次同步後新增的異動 ----------
            logsQuery = logsQuery.Where(log => log.UpdatedAt > lastSyncTime.Value);
        }

        var pendingLogs = await logsQuery
            .OrderBy(log => log.UpdatedAt)
            .ThenBy(log => log.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var changes = new List<SyncChangeDto>(pendingLogs.Count);
        foreach (var log in pendingLogs)
        {
            var change = await BuildChangeDtoAsync(log, cancellationToken);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        // ---------- 將工單異動轉換為舊有回應格式，維持相容性 ----------
        var orders = new List<OrderSyncDto>();
        foreach (var change in changes)
        {
            if (!string.Equals(change.TableName, "orders", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(change.Action, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                // ---------- 刪除操作僅保留在通用變更清單中 ----------
                continue;
            }

            if (change.Payload is null)
            {
                continue;
            }

            try
            {
                var order = change.Payload.Value.Deserialize<OrderSyncDto>(SerializerOptions);
                if (order is not null)
                {
                    orders.Add(order);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析工單同步 Payload 失敗，RecordId: {RecordId}", change.RecordId);
            }
        }

        var normalizedRole = SyncServerRoles.Normalize(remoteServerRole ?? storeType);
        var resolvedIp = remoteIpAddress;
        var storeState = await EnsureStoreStateAsync(storeId, storeType, normalizedRole, resolvedIp, cancellationToken);
        storeState.StoreType = storeType;
        storeState.ServerRole = normalizedRole;
        if (!string.IsNullOrWhiteSpace(resolvedIp))
        {
            storeState.ServerIp = resolvedIp;
        }
        storeState.LastDownloadTime = serverTime;
        if (pendingLogs.Count > 0)
        {
            // ---------- 以最後一筆同步紀錄的識別碼作為游標，方便除錯與後續延伸分頁 ----------
            storeState.LastCursor = pendingLogs[^1].Id.ToString(CultureInfo.InvariantCulture);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SyncDownloadResponse
        {
            StoreId = storeId,
            StoreType = storeType,
            ServerTime = serverTime,
            Changes = changes,
            Orders = orders
        };
    }

    /// <summary>
    /// 依同步紀錄建立回傳給門市的差異資料。
    /// </summary>
    private async Task<SyncChangeDto?> BuildChangeDtoAsync(SyncLog log, CancellationToken cancellationToken)
    {
        var action = log.Action?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        var change = new SyncChangeDto
        {
            TableName = log.TableName,
            Action = action,
            RecordId = log.RecordId,
            UpdatedAt = log.UpdatedAt
        };

        if (string.Equals(action, "DELETE", StringComparison.Ordinal))
        {
            return change;
        }

        if (!string.IsNullOrWhiteSpace(log.Payload))
        {
            try
            {
                // ---------- 優先採用同步紀錄儲存的 Payload，避免資料已刪除時取不到內容 ----------
                using var document = JsonDocument.Parse(log.Payload);
                change.Payload = document.RootElement.Clone();
                return change;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "同步紀錄 Payload 解析失敗，Table: {Table}, RecordId: {RecordId}", log.TableName, log.RecordId);
            }
        }

        var payload = await TryBuildPayloadAsync(log, cancellationToken);
        if (payload.HasValue)
        {
            change.Payload = payload.Value;
        }

        return change;
    }

    /// <summary>
    /// 嘗試取得指定同步紀錄對應的實體內容，並序列化為 JSON。若資料不存在則回傳 null。
    /// </summary>
    private async Task<JsonElement?> TryBuildPayloadAsync(SyncLog log, CancellationToken cancellationToken)
    {
        var entityType = TryResolveEntityType(log.TableName);
        if (entityType is null)
        {
            _logger.LogWarning("找不到資料表 {Table} 對應的實體，無法建立同步 Payload。", log.TableName);
            return null;
        }

        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
        {
            _logger.LogWarning("資料表 {Table} 缺少主鍵設定，無法生成同步 Payload。", log.TableName);
            return null;
        }

        var keySegments = (log.RecordId ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (keySegments.Length != primaryKey.Properties.Count)
        {
            _logger.LogWarning(
                "資料表 {Table} 的主鍵組數與同步紀錄不相符，RecordId: {RecordId}",
                log.TableName,
                log.RecordId);
            return null;
        }

        var keyValues = new object?[keySegments.Length];
        for (var index = 0; index < keySegments.Length; index++)
        {
            var property = primaryKey.Properties[index];
            try
            {
                keyValues[index] = ConvertKeyValue(keySegments[index], property.ClrType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "同步紀錄主鍵轉換失敗，Table: {Table}, Property: {Property}, Value: {Value}",
                    log.TableName,
                    property.Name,
                    keySegments[index]);
                return null;
            }
        }

        var entity = await _dbContext.FindAsync(entityType.ClrType, keyValues);
        if (entity is null)
        {
            _logger.LogWarning(
                "同步紀錄指向的資料不存在，可能已被刪除。Table: {Table}, RecordId: {RecordId}",
                log.TableName,
                log.RecordId);
            return null;
        }

        // ---------- 以實體型別序列化為 JsonElement，讓門市直接反序列化使用 ----------
        return JsonSerializer.SerializeToElement(entity, entityType.ClrType, SerializerOptions);
    }

    /// <summary>
    /// 嘗試由資料表名稱尋找對應的實體描述資訊。
    /// </summary>
    private IEntityType? TryResolveEntityType(string tableName)
    {
        return _dbContext.Model
            .GetEntityTypes()
            .FirstOrDefault(type => string.Equals(type.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 將同步紀錄內的主鍵字串轉換為實際型別，支援常見的整數、GUID 與日期格式。
    /// </summary>
    private static object? ConvertKeyValue(string rawValue, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableType ?? targetType;

        if (actualType == typeof(string))
        {
            return rawValue;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return nullableType is null ? Activator.CreateInstance(actualType) : null;
        }

        if (actualType == typeof(Guid))
        {
            return Guid.Parse(rawValue);
        }

        if (actualType == typeof(DateTime))
        {
            return DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (actualType.IsEnum)
        {
            return Enum.Parse(actualType, rawValue, ignoreCase: true);
        }

        return Convert.ChangeType(rawValue, actualType, CultureInfo.InvariantCulture);
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
    private async Task<StoreSyncState> EnsureStoreStateAsync(string storeId, string storeType, string? serverRole, string? serverIp, CancellationToken cancellationToken)
    {
        var storeState = await _dbContext.StoreSyncStates.FirstOrDefaultAsync(x => x.StoreId == storeId && x.StoreType == storeType, cancellationToken);
        if (storeState is not null)
        {
            if (!string.IsNullOrWhiteSpace(serverRole))
            {
                storeState.ServerRole = serverRole;
            }

            if (!string.IsNullOrWhiteSpace(serverIp))
            {
                storeState.ServerIp = serverIp;
            }

            return storeState;
        }

        storeState = new StoreSyncState
        {
            StoreId = storeId,
            StoreType = storeType,
            ServerRole = serverRole,
            ServerIp = serverIp,
            LastUploadTime = null,
            LastDownloadTime = null
        };
        await _dbContext.StoreSyncStates.AddAsync(storeState, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return storeState;
    }
}
