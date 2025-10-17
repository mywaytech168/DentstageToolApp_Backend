using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    private const string PhotoBinaryField = "fileContentBase64";
    private const string PhotoExtensionField = "fileExtension";
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<SyncService> _logger;
    private readonly PhotoStorageOptions _photoStorageOptions;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public SyncService(
        DentstageToolAppContext dbContext,
        ILogger<SyncService> logger,
        IOptions<PhotoStorageOptions> photoOptions)
    {
        _dbContext = dbContext;
        _logger = logger;
        _photoStorageOptions = photoOptions?.Value ?? new PhotoStorageOptions();
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
        var storeAccount = await EnsureStoreAccountAsync(request.StoreId, request.StoreType, normalizedRole, resolvedIp, cancellationToken);

        if (request.Change is null)
        {
            // ---------- 無異動時提早回應 ----------
            _logger.LogInformation("StoreId {StoreId} 於 {Time} 呼叫同步上傳，但無任何異動紀錄。", request.StoreId, now);
            storeAccount.Role = request.StoreType;
            storeAccount.ServerRole = normalizedRole;
            if (!string.IsNullOrWhiteSpace(resolvedIp))
            {
                storeAccount.ServerIp = resolvedIp;
            }
            storeAccount.LastUploadTime = now;
            storeAccount.LastSyncCount = 0;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        var targetChange = request.Change!;

        _dbContext.DisableSyncLogAutoAppend();
        try
        {
            try
            {
                var processed = await ProcessChangeAsync(targetChange, request.StoreId, request.StoreType, now, cancellationToken);
                if (processed)
                {
                    result.ProcessedCount++;

                    // ---------- 優先沿用門市提供的 SyncLog Id 與 SyncedAt，確保中央與分店紀錄一致 ----------
                    SyncLog? existedLog = null;
                    if (targetChange.LogId.HasValue)
                    {
                        existedLog = await _dbContext.SyncLogs.FindAsync(new object?[] { targetChange.LogId.Value }, cancellationToken);
                    }

                    if (existedLog is null)
                    {
                        var newLog = CreateSyncLog(targetChange, request.StoreId, request.StoreType, now, sequence: 0);
                        await _dbContext.SyncLogs.AddAsync(newLog, cancellationToken);
                    }
                    else
                    {
                        UpdateSyncLog(existedLog, targetChange, request.StoreId, request.StoreType, now, sequence: 0);
                    }
                }
                else
                {
                    result.IgnoredCount++;
                }
            }
            catch (Exception ex)
            {
                // ---------- 單筆處理失敗時立即記錄並回傳忽略狀態 ----------
                _logger.LogError(ex, "處理同步資料時發生例外，StoreId: {StoreId}, RecordId: {RecordId}", request.StoreId, targetChange.RecordId);
                result.IgnoredCount++;
            }

            // ---------- 單筆處理完成後立即同步門市帳號狀態 ----------
            storeAccount.Role = request.StoreType;
            storeAccount.ServerRole = normalizedRole;
            if (!string.IsNullOrWhiteSpace(resolvedIp))
            {
                storeAccount.ServerIp = resolvedIp;
            }
            storeAccount.LastUploadTime = now;
            storeAccount.LastSyncCount = result.ProcessedCount;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            // ---------- 無論成功或失敗都恢復自動產生 Sync Log 的機制 ----------
            _dbContext.EnableSyncLogAutoAppend();
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<SyncDownloadResponse> GetUpdatesAsync(string storeId, string storeType, DateTime? lastSyncTime, string? remoteServerRole, string? remoteIpAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeId))
        {
            throw new ArgumentException("StoreId 不可為空白。", nameof(storeId));
        }

        if (string.IsNullOrWhiteSpace(storeType))
        {
            throw new ArgumentException("StoreType 不可為空白。", nameof(storeType));
        }

        var serverTime = DateTime.UtcNow;
        // ---------- 標準化伺服器角色並先取得門市狀態，後續可使用狀態內的時間戳判斷差異 ----------
        var normalizedRole = SyncServerRoles.Normalize(remoteServerRole ?? storeType);
        var resolvedIp = remoteIpAddress;
        var storeAccount = await EnsureStoreAccountAsync(storeId, storeType, normalizedRole, resolvedIp, cancellationToken);

        // ---------- 以請求與資料庫記錄的最後同步時間綜合判斷上次同步位置 ----------
        var effectiveLastSyncTime = lastSyncTime ?? storeAccount.LastDownloadTime;

        // ---------- 依同步紀錄判斷需要下發的異動 ----------
        var logsQuery = _dbContext.SyncLogs.AsQueryable();

        if (effectiveLastSyncTime.HasValue)
        {
            var syncTime = effectiveLastSyncTime.Value;

            if (syncTime > serverTime)
            {
                // ---------- 若紀錄時間超前於中央伺服器，往前回推十分鐘避免漏資料 ----------
                syncTime = syncTime.AddMinutes(-10);
            }

            logsQuery = logsQuery.Where(log => log.SyncedAt > syncTime);
        }

        var pendingLogs = await logsQuery
            .OrderBy(log => log.SyncedAt)
            .ThenBy(log => log.UpdatedAt)
            .ThenBy(log => log.Id)
            .ToListAsync(cancellationToken);

        // ---------- 只挑選一筆有效同步紀錄回傳，確保門市逐筆處理並立即落盤 ----------
        SyncLog? selectedLog = null;
        SyncChangeDto? selectedChange = null;

        if (pendingLogs.Count > 0)
        {
            // ---------- 預先蒐集候選 LogId，查詢門市是否已下載過 ----------
            var candidateLogIds = pendingLogs
                .Select(log => log.Id)
                .Distinct()
                .ToList();

            var processedLogIds = new HashSet<Guid>();
            if (candidateLogIds.Count > 0)
            {
                var normalizedStoreId = storeId.Trim().ToLowerInvariant();
                var existedLogIds = await _dbContext.SyncLogs
                    .AsNoTracking()
                    .Where(localLog => candidateLogIds.Contains(localLog.Id)
                        && !string.IsNullOrWhiteSpace(localLog.SourceServer))
                    .Select(localLog => new
                    {
                        localLog.Id,
                        Source = localLog.SourceServer!
                    })
                    .ToListAsync(cancellationToken);

                foreach (var item in existedLogIds)
                {
                    if (string.Equals(item.Source, storeId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.Source, normalizedStoreId, StringComparison.OrdinalIgnoreCase))
                    {
                        processedLogIds.Add(item.Id);
                    }
                }
            }

            foreach (var log in pendingLogs)
            {
                // ---------- 若門市曾寫入相同 LogId 代表已處理，直接略過 ----------
                if (processedLogIds.Contains(log.Id))
                {
                    continue;
                }

                // ---------- 檢查來源伺服器是否為門市自身，若是則避免重複派發 ----------
                if (!string.IsNullOrWhiteSpace(log.SourceServer)
                    && string.Equals(log.SourceServer, storeId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var change = await BuildChangeDtoAsync(log, cancellationToken);
                if (change is null)
                {
                    continue;
                }

                selectedLog = log;
                selectedChange = change;
                break;
            }
        }

        // ---------- 若回傳資料為工單且非刪除動作，同步舊有 Orders 欄位以維持相容 ----------
        var orders = new List<OrderSyncDto>();
        if (selectedChange is not null
            && string.Equals(selectedChange.TableName, "orders", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(selectedChange.Action, "DELETE", StringComparison.OrdinalIgnoreCase)
            && selectedChange.Payload is not null)
        {
            try
            {
                var order = selectedChange.Payload.Value.Deserialize<OrderSyncDto>(SerializerOptions);
                if (order is not null)
                {
                    orders.Add(order);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析工單同步 Payload 失敗，RecordId: {RecordId}", selectedChange.RecordId);
            }
        }

        storeAccount.Role = storeType;
        storeAccount.ServerRole = normalizedRole;
        if (!string.IsNullOrWhiteSpace(resolvedIp))
        {
            storeAccount.ServerIp = resolvedIp;
        }

        DateTime ResolveDownloadTime(DateTime reference)
        {
            if (reference <= serverTime)
            {
                return reference;
            }

            var tolerant = reference.AddMinutes(-10);
            if (tolerant > serverTime)
            {
                tolerant = serverTime;
            }

            return tolerant;
        }

        if (selectedLog is not null)
        {
            storeAccount.LastDownloadTime = ResolveDownloadTime(selectedLog.SyncedAt);
            storeAccount.LastSyncCount = 1;
        }
        else if (pendingLogs.Count > 0)
        {
            // ---------- 雖未找到合適資料仍更新檢視位置，避免下一次重複讀取相同紀錄 ----------
            storeAccount.LastDownloadTime = ResolveDownloadTime(pendingLogs[^1].SyncedAt);
            storeAccount.LastSyncCount = 0;
        }
        else
        {
            storeAccount.LastDownloadTime = ResolveDownloadTime(serverTime);
            storeAccount.LastSyncCount = 0;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SyncDownloadResponse
        {
            StoreId = storeId,
            StoreType = storeType,
            ServerTime = serverTime,
            Change = selectedChange,
            Orders = orders
        };
    }

    /// <summary>
    /// 手動建立同步紀錄，供管理端重新派發指定資料。
    /// </summary>
    public async Task<ManualSyncLogResponse> CreateManualSyncLogAsync(ManualSyncLogRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            throw new ArgumentException("TableName 不可為空白。", nameof(request.TableName));
        }

        if (string.IsNullOrWhiteSpace(request.RecordId))
        {
            throw new ArgumentException("RecordId 不可為空白。", nameof(request.RecordId));
        }

        if (string.IsNullOrWhiteSpace(request.StoreId))
        {
            throw new ArgumentException("StoreId 不可為空白。", nameof(request.StoreId));
        }

        if (string.IsNullOrWhiteSpace(request.StoreType))
        {
            throw new ArgumentException("StoreType 不可為空白。", nameof(request.StoreType));
        }

        var normalizedAction = string.IsNullOrWhiteSpace(request.Action)
            ? "UPDATE"
            : request.Action.Trim().ToUpperInvariant();

        if (normalizedAction is not ("INSERT" or "UPDATE" or "UPSERT" or "DELETE"))
        {
            throw new ArgumentException("Action 僅支援 INSERT、UPDATE、UPSERT 或 DELETE。", nameof(request.Action));
        }

        // ---------- 確認門市帳號存在並同步最新的 StoreType 設定 ----------
        await EnsureStoreAccountAsync(request.StoreId, request.StoreType, null, null, cancellationToken);

        var tempLog = new SyncLog
        {
            TableName = request.TableName,
            RecordId = request.RecordId
        };

        var payloadElement = await TryBuildPayloadAsync(tempLog, cancellationToken);
        var payloadJson = payloadElement?.GetRawText();

        var now = DateTime.UtcNow;
        var log = new SyncLog
        {
            Id = Guid.NewGuid(),
            TableName = request.TableName,
            RecordId = request.RecordId,
            Action = normalizedAction,
            UpdatedAt = now,
            SyncedAt = now,
            SourceServer = request.StoreId,
            StoreType = request.StoreType,
            Synced = false,
            Payload = payloadJson
        };

        await _dbContext.SyncLogs.AddAsync(log, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManualSyncLogResponse
        {
            LogId = log.Id,
            SyncedAt = log.SyncedAt
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
            LogId = log.Id,
            TableName = log.TableName,
            Action = action,
            RecordId = log.RecordId,
            UpdatedAt = log.UpdatedAt,
            SyncedAt = log.SyncedAt
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
    /// 將分店上傳的同步異動轉換為中央儲存的 Sync Log。
    /// </summary>
    private static SyncLog CreateSyncLog(SyncChangeDto change, string storeId, string storeType, DateTime fallbackTime, int sequence)
    {
        var payload = change.Payload.HasValue ? change.Payload.Value.GetRawText() : null;
        var action = change.Action?.Trim().ToUpperInvariant() ?? string.Empty;
        // ---------- 若門市提供同步時間則沿用，否則退回資料的 UpdatedAt 或伺服器時間 ----------
        var syncedAt = change.SyncedAt ?? change.UpdatedAt ?? fallbackTime.AddTicks(sequence);
        var updatedAt = change.UpdatedAt ?? syncedAt;
        var logId = change.LogId ?? Guid.NewGuid();

        return new SyncLog
        {
            Id = logId,
            TableName = change.TableName,
            RecordId = change.RecordId,
            Action = action,
            UpdatedAt = updatedAt,
            SyncedAt = syncedAt,
            SourceServer = storeId,
            StoreType = storeType,
            Synced = true,
            Payload = payload
        };
    }

    /// <summary>
    /// 更新既有同步紀錄的欄位內容，讓中央與門市保持一致。
    /// </summary>
    private static void UpdateSyncLog(SyncLog log, SyncChangeDto change, string storeId, string storeType, DateTime fallbackTime, int sequence)
    {
        var payload = change.Payload.HasValue ? change.Payload.Value.GetRawText() : null;
        var action = change.Action?.Trim().ToUpperInvariant() ?? string.Empty;

        // ---------- 門市若提供 SyncedAt 與 UpdatedAt 則優先採用，否則沿用舊值或伺服器時間 ----------
        var syncedAt = change.SyncedAt ?? (log.SyncedAt != default ? log.SyncedAt : change.UpdatedAt) ?? fallbackTime.AddTicks(sequence);
        var updatedAt = change.UpdatedAt ?? (log.UpdatedAt != default ? log.UpdatedAt : syncedAt);

        log.TableName = change.TableName;
        log.RecordId = change.RecordId;
        log.Action = action;
        log.UpdatedAt = updatedAt;
        log.SyncedAt = syncedAt;
        log.SourceServer = storeId;
        log.StoreType = storeType;
        log.Synced = true;
        log.Payload = payload;
    }

    /// <summary>
    /// 嘗試取得指定同步紀錄對應的實體內容，並序列化為 JSON。若資料不存在則回傳 null。
    /// </summary>
    private async Task<JsonElement?> TryBuildPayloadAsync(SyncLog log, CancellationToken cancellationToken)
    {
        if (string.Equals(log.TableName, "photo_data", StringComparison.OrdinalIgnoreCase))
        {
            return await BuildPhotoPayloadAsync(log.RecordId, cancellationToken);
        }

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
    /// 建立照片同步所需的 Payload，並附帶檔案內容。
    /// </summary>
    private async Task<JsonElement?> BuildPhotoPayloadAsync(string? photoUid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(photoUid))
        {
            return null;
        }

        var photo = await _dbContext.PhotoData
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.PhotoUid == photoUid, cancellationToken);

        if (photo is null)
        {
            _logger.LogWarning("找不到照片 {PhotoUid} 的資料庫紀錄，無法回傳 Payload。", photoUid);
            return null;
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["photoUid"] = photo.PhotoUid,
            ["quotationUid"] = photo.QuotationUid,
            ["relatedUid"] = photo.RelatedUid,
            ["posion"] = photo.Posion,
            ["comment"] = photo.Comment,
            ["photoShape"] = photo.PhotoShape,
            ["photoShapeOther"] = photo.PhotoShapeOther,
            ["photoShapeShow"] = photo.PhotoShapeShow,
            ["cost"] = photo.Cost,
            ["flagFinish"] = photo.FlagFinish,
            ["finishCost"] = photo.FinishCost
        };

        var storageRoot = EnsurePhotoStorageRoot();
        var physicalPath = ResolvePhotoPhysicalPath(storageRoot, photo.PhotoUid);

        if (File.Exists(physicalPath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(physicalPath, cancellationToken);
                payload[PhotoBinaryField] = Convert.ToBase64String(bytes);

                var extension = Path.GetExtension(physicalPath);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    payload[PhotoExtensionField] = extension;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "讀取照片檔案 {PhotoUid} 失敗，將僅回傳欄位資料。", photoUid);
            }
        }
        else
        {
            _logger.LogWarning("找不到照片 {PhotoUid} 的實體檔案，路徑：{Path}", photoUid, physicalPath);
        }

        return JsonSerializer.SerializeToElement(payload, SerializerOptions);
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
    /// 儲存同步上傳的照片檔案，必要時更新副檔名並清理舊檔案。
    /// </summary>
    private async Task SavePhotoFileAsync(string photoUid, string base64Content, string? extension, CancellationToken cancellationToken)
    {
        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(base64Content);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "照片 {PhotoUid} 的檔案內容不是有效的 Base64 字串。", photoUid);
            return;
        }

        var storageRoot = EnsurePhotoStorageRoot();
        var normalizedExtension = NormalizePhotoExtension(extension, storageRoot, photoUid);
        var targetPath = Path.Combine(storageRoot, photoUid + normalizedExtension);

        foreach (var existed in Directory.EnumerateFiles(storageRoot, photoUid + ".*", SearchOption.TopDirectoryOnly))
        {
            if (!string.Equals(existed, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(existed);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理舊照片檔案失敗：{Path}", existed);
                }
            }
        }

        await File.WriteAllBytesAsync(targetPath, fileBytes, cancellationToken);
    }

    /// <summary>
    /// 嘗試移除指定照片的實體檔案。
    /// </summary>
    private void TryDeletePhotoFile(string photoUid)
    {
        var storageRoot = EnsurePhotoStorageRoot();
        foreach (var path in Directory.EnumerateFiles(storageRoot, photoUid + ".*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "刪除照片 {PhotoUid} 檔案失敗，路徑：{Path}", photoUid, path);
            }
        }
    }

    /// <summary>
    /// 取得照片儲存根目錄，若不存在則自動建立。
    /// </summary>
    private string EnsurePhotoStorageRoot()
    {
        var root = _photoStorageOptions.RootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "App_Data", "photos");
        }

        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
        }

        return root;
    }

    /// <summary>
    /// 由 PhotoUid 取得實際檔案路徑，支援不同副檔名。
    /// </summary>
    private static string ResolvePhotoPhysicalPath(string storageRoot, string photoUid)
    {
        var candidate = Directory.EnumerateFiles(storageRoot, photoUid + ".*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        return Path.Combine(storageRoot, photoUid);
    }

    /// <summary>
    /// 規範化照片副檔名，若缺少則參考既有檔案或預設為 .jpg。
    /// </summary>
    private static string NormalizePhotoExtension(string? extension, string storageRoot, string photoUid)
    {
        if (!string.IsNullOrWhiteSpace(extension))
        {
            extension = extension.Trim();
            return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
        }

        var existed = Directory.EnumerateFiles(storageRoot, photoUid + ".*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(existed))
        {
            return Path.GetExtension(existed);
        }

        return ".jpg";
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
    /// 處理照片資料同步，將資料庫欄位與實際檔案一併更新。
    /// </summary>
    private async Task<bool> ProcessPhotoChangeAsync(string action, SyncChangeDto change, string storeId, DateTime processTime, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "DELETE":
                var existedPhoto = await _dbContext.PhotoData.FirstOrDefaultAsync(x => x.PhotoUid == change.RecordId, cancellationToken);
                if (existedPhoto is not null)
                {
                    _dbContext.PhotoData.Remove(existedPhoto);
                    TryDeletePhotoFile(existedPhoto.PhotoUid);
                }
                return true;

            case "INSERT":
            case "UPDATE":
            case "UPSERT":
                if (change.Payload is null || change.Payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    _logger.LogWarning("照片同步缺少 Payload，RecordId: {RecordId}", change.RecordId);
                    return false;
                }

                PhotoSyncPayload? payload;
                try
                {
                    payload = change.Payload.Value.Deserialize<PhotoSyncPayload>(SerializerOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "照片同步 Payload 解析失敗，RecordId: {RecordId}", change.RecordId);
                    return false;
                }

                if (payload is null || string.IsNullOrWhiteSpace(payload.PhotoUid))
                {
                    _logger.LogWarning("照片同步缺少 PhotoUid，RecordId: {RecordId}", change.RecordId);
                    return false;
                }

                var photo = await _dbContext.PhotoData.FirstOrDefaultAsync(x => x.PhotoUid == payload.PhotoUid, cancellationToken);
                if (photo is null)
                {
                    photo = new PhotoDatum
                    {
                        PhotoUid = payload.PhotoUid
                    };
                    await _dbContext.PhotoData.AddAsync(photo, cancellationToken);
                }

                photo.QuotationUid = payload.QuotationUid;
                photo.RelatedUid = payload.RelatedUid;
                photo.Posion = payload.Posion;
                photo.Comment = payload.Comment;
                photo.PhotoShape = payload.PhotoShape;
                photo.PhotoShapeOther = payload.PhotoShapeOther;
                photo.PhotoShapeShow = payload.PhotoShapeShow;
                photo.Cost = payload.Cost;
                photo.FlagFinish = payload.FlagFinish;
                photo.FinishCost = payload.FinishCost;

                if (!string.IsNullOrWhiteSpace(payload.FileContentBase64))
                {
                    await SavePhotoFileAsync(payload.PhotoUid, payload.FileContentBase64, payload.FileExtension, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("照片 {PhotoUid} 同步未附帶檔案內容，將沿用既有檔案。", payload.PhotoUid);
                }

                return true;

            default:
                _logger.LogWarning("照片同步收到未支援的動作 {Action}，RecordId: {RecordId}", action, change.RecordId);
                return false;
        }
    }

    /// <summary>
    /// 依據通用同步資料套用至對應資料表，回傳是否成功處理。
    /// </summary>
    private async Task<bool> ProcessChangeAsync(
        SyncChangeDto change,
        string storeId,
        string storeType,
        DateTime processTime,
        CancellationToken cancellationToken)
    {
        // ---------- 驗證基本欄位，避免 Null 造成例外 ----------
        if (string.IsNullOrWhiteSpace(change.TableName))
        {
            _logger.LogWarning("同步資料缺少 TableName，RecordId: {RecordId}", change.RecordId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(change.RecordId))
        {
            _logger.LogWarning("同步資料缺少 RecordId，Table: {Table}", change.TableName);
            return false;
        }

        var action = change.Action?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(action))
        {
            _logger.LogWarning("同步資料缺少 Action，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
            return false;
        }

        // ---------- 工單維持原有特殊邏輯 ----------
        if (string.Equals(change.TableName, "orders", StringComparison.OrdinalIgnoreCase))
        {
            if (change.Payload is null || change.Payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (!string.Equals(action, "DELETE", StringComparison.Ordinal))
                {
                    _logger.LogWarning("工單同步缺少 Payload，RecordId: {RecordId}", change.RecordId);
                    return false;
                }
            }

            await ProcessOrderChangeAsync(action, change, storeId, storeType, processTime, cancellationToken);
            return true;
        }

        if (string.Equals(change.TableName, "photo_data", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessPhotoChangeAsync(action, change, storeId, processTime, cancellationToken);
        }

        var entityType = TryResolveEntityType(change.TableName);
        if (entityType is null)
        {
            _logger.LogWarning("找不到資料表 {Table} 對應的實體，RecordId: {RecordId}", change.TableName, change.RecordId);
            return false;
        }

        if (!TryParseKeyValues(entityType, change.RecordId, out var keyValues))
        {
            _logger.LogWarning("無法解析同步資料主鍵，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
            return false;
        }

        switch (action)
        {
            case "INSERT":
            case "UPDATE":
            case "UPSERT":
                if (change.Payload is null || change.Payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    _logger.LogWarning("同步 {Action} 異動缺少 Payload，Table: {Table}, RecordId: {RecordId}", action, change.TableName, change.RecordId);
                    return false;
                }

                object? payloadEntity;
                try
                {
                    payloadEntity = change.Payload.Value.Deserialize(entityType.ClrType, SerializerOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "同步 Payload 反序列化失敗，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
                    return false;
                }

                if (payloadEntity is null)
                {
                    _logger.LogWarning("同步 Payload 解析後為空，Table: {Table}, RecordId: {RecordId}", change.TableName, change.RecordId);
                    return false;
                }

                var existedEntity = await _dbContext.FindAsync(entityType.ClrType, keyValues);
                if (existedEntity is null)
                {
                    // ---------- 找不到舊資料時直接新增，確保中央資料完整 ----------
                    _dbContext.Add(payloadEntity);
                }
                else
                {
                    // ---------- 使用 EF Core 套用欄位，避免逐一指定造成遺漏 ----------
                    _dbContext.Entry(existedEntity).CurrentValues.SetValues(payloadEntity);
                }

                return true;

            case "DELETE":
                var entity = await _dbContext.FindAsync(entityType.ClrType, keyValues);
                if (entity is not null)
                {
                    // ---------- 找到資料才進行刪除，避免多餘例外 ----------
                    _dbContext.Remove(entity);
                }

                return true;

            default:
                _logger.LogWarning("同步資料包含未支援動作 {Action}，Table: {Table}", action, change.TableName);
                return false;
        }
    }

    /// <summary>
    /// 解析同步紀錄的主鍵字串為實際型別陣列。
    /// </summary>
    private static bool TryParseKeyValues(IEntityType entityType, string? recordId, out object?[] keyValues)
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
            catch
            {
                return false;
            }
        }

        return true;
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
    private async Task<UserAccount> EnsureStoreAccountAsync(string storeId, string storeType, string? serverRole, string? serverIp, CancellationToken cancellationToken)
    {
        var account = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.UserUid == storeId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException($"找不到 StoreId {storeId} 對應的使用者帳號，請先建立門市帳號。");
        }

        if (!string.IsNullOrWhiteSpace(serverRole))
        {
            account.ServerRole = serverRole;
        }

        if (!string.IsNullOrWhiteSpace(serverIp))
        {
            account.ServerIp = serverIp;
        }

        if (string.IsNullOrWhiteSpace(account.Role) && !string.IsNullOrWhiteSpace(storeType))
        {
            account.Role = storeType;
        }

        return account;
    }
}
