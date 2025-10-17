using System;
using DentstageToolApp.Api.Models.Auth;
using DentstageToolApp.Api.Models.Sync;

namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// 同步機制的組態選項，提供伺服器角色與排程設定。
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// Token 欄位的同步鎖，確保多執行緒同時讀寫時不會互相覆蓋。
    /// </summary>
    private readonly object _tokenLock = new();

    /// <summary>
    /// 目前記憶體內保存的 Access Token，供同步呼叫帶入授權標頭。
    /// </summary>
    private string? _accessToken;

    /// <summary>
    /// Access Token 的到期時間。
    /// </summary>
    private DateTime? _accessTokenExpireAt;

    /// <summary>
    /// Refresh Token 字串，預留給後續需要延長授權時使用。
    /// </summary>
    private string? _refreshToken;

    /// <summary>
    /// Refresh Token 的到期時間。
    /// </summary>
    private DateTime? _refreshTokenExpireAt;

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
    /// 門市型態（例如直營店、連盟店），供呼叫同步 API 時使用。
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
    /// 中央伺服器 API 根網址，背景排程會透過此網址呼叫同步端點。
    /// </summary>
    public string? CentralApiBaseUrl { get; set; }

    /// <summary>
    /// 由資料庫推導出的中央伺服器 IP，門市可用來動態判斷連線設定。
    /// </summary>
    public string? CentralServerIp { get; set; }

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
    /// 取得目前的 Access Token，若尚未登入則回傳 null。
    /// </summary>
    public string? GetAccessToken()
    {
        lock (_tokenLock)
        {
            return _accessToken;
        }
    }

    /// <summary>
    /// 判斷 Access Token 是否仍在有效期限內，預設保留 1 分鐘安全緩衝時間。
    /// </summary>
    public bool HasValidAccessToken(TimeSpan? safetyMargin = null)
    {
        lock (_tokenLock)
        {
            if (string.IsNullOrWhiteSpace(_accessToken) || !_accessTokenExpireAt.HasValue)
            {
                return false;
            }

            var margin = safetyMargin ?? TimeSpan.FromMinutes(1);
            var threshold = _accessTokenExpireAt.Value - margin;
            return threshold > DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 判斷 Refresh Token 是否有效，供未來擴充自動換發權杖時使用。
    /// </summary>
    public bool HasValidRefreshToken(TimeSpan? safetyMargin = null)
    {
        lock (_tokenLock)
        {
            if (string.IsNullOrWhiteSpace(_refreshToken) || !_refreshTokenExpireAt.HasValue)
            {
                return false;
            }

            var margin = safetyMargin ?? TimeSpan.FromMinutes(1);
            var threshold = _refreshTokenExpireAt.Value - margin;
            return threshold > DateTime.UtcNow;
        }
    }

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
    /// 判斷目前設定是否為同步支援角色（中央或分店）。
    /// </summary>
    public bool IsStoreRole => SyncServerRoles.IsStoreRole(ServerRole);

    /// <summary>
    /// 判斷目前設定是否為門市角色（直營或連盟）。
    /// </summary>
    public bool IsBranchRole => SyncServerRoles.IsBranchRole(ServerRole);

    /// <summary>
    /// 判斷目前設定是否為中央角色
    /// </summary>
    public bool IsCentralRole => SyncServerRoles.IsCentralRole(ServerRole);

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

    /// <summary>
    /// 依登入回應更新權杖與門市資料，確保背景同步呼叫使用最新資訊。
    /// </summary>
    public void ApplyLoginResponse(LoginResponse response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        ApplyMachineProfile(response.ServerRole, response.StoreId, response.StoreType);
        UpdateAuthTokens(response.AccessToken, response.AccessTokenExpireAt, response.RefreshToken, response.RefreshTokenExpireAt);
    }

    /// <summary>
    /// 清除記憶體中的權杖資訊，通常在授權失敗或重新登入前呼叫。
    /// </summary>
    public void ClearAuthTokens()
    {
        UpdateAuthTokens(null, null, null, null);
    }

    /// <summary>
    /// 更新 Access Token 與 Refresh Token 的內容與到期時間。
    /// </summary>
    public void UpdateAuthTokens(string? accessToken, DateTime? accessTokenExpireAt, string? refreshToken, DateTime? refreshTokenExpireAt)
    {
        lock (_tokenLock)
        {
            _accessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken;
            _accessTokenExpireAt = accessTokenExpireAt;
            _refreshToken = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken;
            _refreshTokenExpireAt = refreshTokenExpireAt;
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
