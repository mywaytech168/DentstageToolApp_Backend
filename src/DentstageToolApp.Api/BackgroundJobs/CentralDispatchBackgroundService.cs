using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Api.Services.Sync;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.BackgroundJobs;

/// <summary>
/// 門市端負責取得中央下發資料的背景服務，定期向中央伺服器請求差異並套用至本地資料庫。
/// </summary>
public class CentralDispatchBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRemoteSyncApiClient _remoteSyncApiClient;
    private readonly ILogger<CentralDispatchBackgroundService> _logger;
    private readonly SyncOptions _syncOptions;

    /// <summary>
    /// 建構子，注入所需的同步服務與設定。
    /// </summary>
    public CentralDispatchBackgroundService(
        IServiceScopeFactory scopeFactory,
        IRemoteSyncApiClient remoteSyncApiClient,
        IOptions<SyncOptions> syncOptions,
        ILogger<CentralDispatchBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _remoteSyncApiClient = remoteSyncApiClient;
        _logger = logger;
        _syncOptions = syncOptions.Value ?? throw new ArgumentNullException(nameof(syncOptions));
    }

    /// <summary>
    /// 只有門市角色才需要啟動中央下發背景工作。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var normalizedRole = _syncOptions.NormalizedServerRole;
        if (!SyncServerRoles.IsStoreRole(normalizedRole))
        {
            _logger.LogInformation("目前伺服器角色為 {Role}，不啟動中央下發背景工作。", string.IsNullOrWhiteSpace(normalizedRole) ? "未設定" : normalizedRole);
            return;
        }

        if (!_syncOptions.HasResolvedMachineProfile)
        {
            _logger.LogWarning("缺少門市識別資訊，無法啟動中央下發背景工作，請確認 SyncMachineProfiles 設定。");
            return;
        }

        if (string.IsNullOrWhiteSpace(_syncOptions.StoreId) || string.IsNullOrWhiteSpace(_syncOptions.StoreType))
        {
            _logger.LogWarning("SyncOptions 缺少 StoreId 或 StoreType，無法向中央請求資料。");
            return;
        }

        var intervalMinutes = _syncOptions.BackgroundSyncIntervalMinutes <= 0
            ? 60
            : _syncOptions.BackgroundSyncIntervalMinutes;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        _logger.LogInformation("中央下發背景工作啟動，每 {Interval} 分鐘檢查中央差異資料。", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunDispatchCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ---------- 背景工作停止，離開迴圈 ----------
                break;
            }
        }
    }

    /// <summary>
    /// 執行單次中央下發流程：向中央請求資料並更新本地資料庫。
    /// </summary>
    private async Task RunDispatchCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();

            var storeId = _syncOptions.StoreId!;
            var storeType = _syncOptions.StoreType!;
            var serverRole = _syncOptions.NormalizedServerRole;

            var storeState = await dbContext.StoreSyncStates
                .FirstOrDefaultAsync(state => state.StoreId == storeId && state.StoreType == storeType, cancellationToken);

            var query = new SyncDownloadQuery
            {
                StoreId = storeId,
                StoreType = storeType,
                ServerRole = serverRole,
                LastSyncTime = storeState?.LastDownloadTime,
                PageSize = _syncOptions.BackgroundSyncBatchSize <= 0 ? 100 : _syncOptions.BackgroundSyncBatchSize
            };

            var response = await _remoteSyncApiClient.GetUpdatesAsync(query, cancellationToken);
            if (response is null)
            {
                _logger.LogWarning("中央下發背景工作呼叫中央 API 失敗，StoreId: {StoreId}, StoreType: {StoreType}", storeId, storeType);
                return;
            }

            // ---------- 先標記所有後續異動為中央來源，方便同步紀錄辨識 ----------
            dbContext.SetSyncLogMetadata(SyncServerRoles.CentralServer, storeType);

            var changes = response.Changes ?? new List<SyncChangeDto>();
            if (changes.Count == 0 && response.Orders.Count == 0)
            {
                await UpdateStoreStateAsync(dbContext, storeState, storeId, storeType, serverRole, response.ServerTime, cancellationToken);
                await MarkCentralLogsAsSyncedAsync(dbContext, cancellationToken);
                _logger.LogInformation("中央下發背景工作完成，本次無差異資料。StoreId: {StoreId}", storeId);
                return;
            }

            if (changes.Count > 0)
            {
                foreach (var change in changes)
                {
                    await ApplyChangeAsync(dbContext, change, cancellationToken);
                }
            }
            else
            {
                // ---------- 若中央尚未提供通用異動格式，沿用舊有工單流程 ----------
                foreach (var orderDto in response.Orders)
                {
                    var order = await dbContext.Orders
                        .FirstOrDefaultAsync(entity => entity.OrderUid == orderDto.OrderUid, cancellationToken);

                    if (order is null)
                    {
                        order = new Order
                        {
                            OrderUid = orderDto.OrderUid
                        };

                        dbContext.Orders.Add(order);
                    }

                    ApplyOrderSyncData(order, orderDto);
                }
            }

            await UpdateStoreStateAsync(dbContext, storeState, storeId, storeType, serverRole, response.ServerTime, cancellationToken);

            await MarkCentralLogsAsSyncedAsync(dbContext, cancellationToken);

            var processedCount = changes.Count > 0 ? changes.Count : response.Orders.Count;
            _logger.LogInformation(
                "中央下發背景工作完成，StoreId: {StoreId}, 同步筆數: {Count}",
                storeId,
                processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "中央下發背景工作執行失敗，StoreId: {StoreId}, StoreType: {StoreType}", _syncOptions.StoreId, _syncOptions.StoreType);
        }
    }

    /// <summary>
    /// 依中央回傳的異動資訊更新本地資料庫，支援新增、更新與刪除。
    /// </summary>
    private async Task ApplyChangeAsync(DentstageToolAppContext dbContext, SyncChangeDto change, CancellationToken cancellationToken)
    {
        var action = change.Action?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(action))
        {
            _logger.LogWarning("中央下發資料缺少動作類型，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
            return;
        }

        var entityType = TryResolveEntityType(dbContext, change.TableName);
        if (entityType is null)
        {
            _logger.LogWarning("找不到資料表 {Table} 對應的實體，略過中央下發。", change.TableName);
            return;
        }

        if (!TryParseKeyValues(entityType, change.RecordId, out var keyValues))
        {
            _logger.LogWarning("解析中央下發紀錄主鍵失敗，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
            return;
        }

        switch (action)
        {
            case "INSERT":
            case "UPDATE":
            case "UPSERT":
                if (change.Payload is null)
                {
                    _logger.LogWarning("中央下發 {Action} 異動缺少 Payload，Table: {Table}, RecordId: {RecordId}", action, change.TableName, change.RecordId);
                    return;
                }

                object? payloadEntity;
                try
                {
                    payloadEntity = change.Payload.Value.Deserialize(entityType.ClrType, SerializerOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "中央下發 Payload 反序列化失敗，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
                    return;
                }

                if (payloadEntity is null)
                {
                    _logger.LogWarning("中央下發 Payload 反序列化結果為空，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
                    return;
                }

                var existedEntity = await dbContext.FindAsync(entityType.ClrType, keyValues);
                if (existedEntity is null)
                {
                    // ---------- 找不到舊資料時改為新增 ----------
                    dbContext.Add(payloadEntity);
                }
                else
                {
                    dbContext.Entry(existedEntity).CurrentValues.SetValues(payloadEntity);
                }

                break;

            case "DELETE":
                var entity = await dbContext.FindAsync(entityType.ClrType, keyValues);
                if (entity is not null)
                {
                    dbContext.Remove(entity);
                }

                break;

            default:
                _logger.LogWarning("中央下發資料包含未支援的動作：{Action}", action);
                break;
        }
    }

    /// <summary>
    /// 將中央回傳的工單資料套用至本地實體。
    /// </summary>
    private static void ApplyOrderSyncData(Order order, OrderSyncDto dto)
    {
        order.OrderNo = dto.OrderNo;
        order.StoreUid = dto.StoreUid;
        order.Amount = dto.Amount;
        order.Status = dto.Status;
        order.CreationTimestamp = dto.CreationTimestamp;
        order.ModificationTimestamp = dto.ModificationTimestamp;
        order.QuatationUid = dto.QuatationUid;
        order.CreatedBy = dto.CreatedBy;
        order.ModifiedBy = dto.ModifiedBy;
    }

    /// <summary>
    /// 更新門市同步狀態，包含最後下載時間與伺服器資訊。
    /// </summary>
    private async Task UpdateStoreStateAsync(
        DentstageToolAppContext dbContext,
        StoreSyncState? storeState,
        string storeId,
        string storeType,
        string serverRole,
        DateTime serverTime,
        CancellationToken cancellationToken)
    {
        storeState ??= new StoreSyncState
        {
            StoreId = storeId,
            StoreType = storeType
        };

        if (storeState.Id == 0)
        {
            dbContext.StoreSyncStates.Add(storeState);
        }

        storeState.ServerRole = serverRole;
        if (!string.IsNullOrWhiteSpace(_syncOptions.ServerIp))
        {
            storeState.ServerIp = _syncOptions.ServerIp;
        }

        storeState.LastDownloadTime = serverTime;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 將中央來源產生的同步紀錄標記為已完成，避免下一輪重複上傳。
    /// </summary>
    private static async Task MarkCentralLogsAsSyncedAsync(DentstageToolAppContext dbContext, CancellationToken cancellationToken)
    {
        var centralLogs = await dbContext.SyncLogs
            .Where(log => !log.Synced && string.Equals(log.SourceServer, SyncServerRoles.CentralServer, StringComparison.Ordinal))
            .ToListAsync(cancellationToken);

        if (centralLogs.Count == 0)
        {
            return;
        }

        foreach (var log in centralLogs)
        {
            log.Synced = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 嘗試解析同步紀錄的主鍵內容，回傳實際型別陣列供查詢使用。
    /// </summary>
    private bool TryParseKeyValues(IEntityType entityType, string? recordId, out object?[] keyValues)
    {
        keyValues = Array.Empty<object?>();
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
        {
            return false;
        }

        var segments = (recordId ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (segments.Length != primaryKey.Properties.Count)
        {
            return false;
        }

        keyValues = new object?[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            try
            {
                keyValues[index] = ConvertKeyValue(segments[index], primaryKey.Properties[index].ClrType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "中央下發主鍵值轉換失敗，Table: {Table}, Property: {Property}, Value: {Value}",
                    entityType.GetTableName(),
                    primaryKey.Properties[index].Name,
                    segments[index]);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 依資料表名稱尋找對應的實體資訊。
    /// </summary>
    private static IEntityType? TryResolveEntityType(DentstageToolAppContext dbContext, string tableName)
    {
        return dbContext.Model
            .GetEntityTypes()
            .FirstOrDefault(type => string.Equals(type.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 將文字型主鍵值轉換為資料模型所需的實際型別。
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
}
