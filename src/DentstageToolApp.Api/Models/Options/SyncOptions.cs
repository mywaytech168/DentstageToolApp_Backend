using System;
using DentstageToolApp.Api.Models.Sync;

namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// 同步機制的組態選項，提供伺服器角色與排程設定。
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// 伺服器角色，分為中央伺服器、直營門市或連盟門市。
    /// </summary>
    public string? ServerRole { get; set; }

    /// <summary>
    /// 同步機碼，啟動時會透過此機碼向資料庫查詢伺服器角色與門市資訊。
    /// </summary>
    public string? MachineKey { get; set; }

    /// <summary>
    /// 門市識別碼，僅在直營或連盟門市角色時需要設定。
    /// </summary>
    public string? StoreId { get; set; }

    /// <summary>
    /// 門市型態（例如 Direct、Franchise），供呼叫同步 API 時使用。
    /// </summary>
    public string? StoreType { get; set; }

    /// <summary>
    /// 目前伺服器對外可辨識的 IP，中央可用來建立 store_sync_states 的來源資訊。
    /// </summary>
    public string? ServerIp { get; set; }

    /// <summary>
    /// 同步通訊管道，可選擇 Http 或 RabbitMq 佇列。
    /// </summary>
    public string Transport { get; set; } = SyncTransportModes.Http;

    /// <summary>
    /// RabbitMQ 佇列設定，僅在 Transport 為 RabbitMq 時使用。
    /// </summary>
    public SyncQueueOptions Queue { get; set; } = new();

    /// <summary>
    /// 背景同步的排程間隔（分鐘），預設為 60 分鐘執行一次。
    /// </summary>
    public int BackgroundSyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 單次背景同步要處理的最大筆數，避免一次讀取過多資料造成效能問題。
    /// </summary>
    public int BackgroundSyncBatchSize { get; set; } = 100;

    /// <summary>
    /// 判斷是否已透過機碼解析出必要的門市資訊。
    /// </summary>
    public bool HasResolvedMachineProfile
    {
        get
        {
            var role = NormalizedServerRole;
            if (string.IsNullOrWhiteSpace(role))
            {
                return false;
            }

            if (string.Equals(role, SyncServerRoles.CentralServer, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(StoreId) && !string.IsNullOrWhiteSpace(StoreType);
        }
    }

    /// <summary>
    /// 將伺服器角色轉換為既定常數，避免大小寫造成判斷差異。
    /// </summary>
    public string NormalizedServerRole => SyncServerRoles.Normalize(ServerRole);

    /// <summary>
    /// 判斷目前設定是否為門市角色（直營或連盟）。
    /// </summary>
    public bool IsStoreRole => SyncServerRoles.IsStoreRole(ServerRole);

    /// <summary>
    /// 判斷是否使用訊息佇列作為同步通訊方式。
    /// </summary>
    public bool UseMessageQueue => string.Equals(Transport, SyncTransportModes.RabbitMq, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 以資料庫查詢結果套用同步機碼設定，補齊伺服器角色與門市資訊。
    /// </summary>
    public void ApplyMachineProfile(string? serverRole, string? storeId, string? storeType)
    {
        if (!string.IsNullOrWhiteSpace(serverRole))
        {
            ServerRole = serverRole;
        }

        if (!string.IsNullOrWhiteSpace(storeId))
        {
            StoreId = storeId;
        }

        if (!string.IsNullOrWhiteSpace(storeType))
        {
            StoreType = storeType;
        }
    }
}

/// <summary>
/// 定義 RabbitMQ 相關連線與佇列設定。
/// </summary>
public class SyncQueueOptions
{
    /// <summary>
    /// RabbitMQ 主機位置。
    /// </summary>
    public string? HostName { get; set; }

    /// <summary>
    /// RabbitMQ Virtual Host 名稱。
    /// </summary>
    public string? VirtualHost { get; set; }

    /// <summary>
    /// RabbitMQ 帳號。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// RabbitMQ 密碼。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 用於上傳同步請求的佇列名稱。
    /// </summary>
    public string? RequestQueue { get; set; }

    /// <summary>
    /// 取得中央回應資料時使用的佇列名稱。
    /// </summary>
    public string? ResponseQueue { get; set; }

    /// <summary>
    /// 等待回應的逾時秒數，預設 30 秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
