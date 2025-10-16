using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.Services.Sync;

/// <summary>
/// 透過 HttpClient 呼叫中央伺服器同步 API 的實作。
/// </summary>
public class RemoteSyncApiClient : IRemoteSyncApiClient
{
    private const string UploadEndpoint = "/api/sync/upload";
    private const string DownloadEndpoint = "/api/sync/changes";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<RemoteSyncApiClient> _logger;
    private readonly SyncOptions _syncOptions;

    /// <summary>
    /// 建構子，設定 HttpClient 與同步組態。
    /// </summary>
    public RemoteSyncApiClient(HttpClient httpClient, IOptions<SyncOptions> syncOptions, ILogger<RemoteSyncApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _syncOptions = syncOptions.Value;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_syncOptions.CentralApiBaseUrl))
        {
            // ---------- 初始化 HttpClient 的 BaseAddress，避免後續呼叫需自行組裝完整網址 ----------
            _httpClient.BaseAddress = new Uri(_syncOptions.CentralApiBaseUrl, UriKind.Absolute);
        }
    }

    /// <inheritdoc />
    public async Task<SyncUploadResult?> UploadChangesAsync(SyncUploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(UploadEndpoint, request, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "呼叫中央同步上傳 API 失敗，狀態碼: {StatusCode}, 訊息: {Message}",
                    response.StatusCode,
                    string.IsNullOrWhiteSpace(message) ? "<空白>" : message);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SyncUploadResult>(SerializerOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ---------- 外部取消呼叫，直接往外拋出即可 ----------
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "呼叫中央同步上傳 API 時發生例外。");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SyncDownloadResponse?> GetUpdatesAsync(SyncDownloadQuery query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (string.IsNullOrWhiteSpace(_syncOptions.CentralApiBaseUrl))
        {
            _logger.LogWarning("未設定中央 API 根網址，無法下載同步資料。");
            return null;
        }

        try
        {
            var parameters = new Dictionary<string, string?>
            {
                ["storeId"] = query.StoreId,
                ["storeType"] = query.StoreType,
                ["pageSize"] = query.PageSize.ToString(),
                ["serverRole"] = query.ServerRole,
                ["lastSyncTime"] = query.LastSyncTime?.ToString("O")
            };

            var requestUri = QueryHelpers.AddQueryString(DownloadEndpoint, parameters);
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "呼叫中央同步下載 API 失敗，狀態碼: {StatusCode}, 訊息: {Message}",
                    response.StatusCode,
                    string.IsNullOrWhiteSpace(message) ? "<空白>" : message);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SyncDownloadResponse>(SerializerOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ---------- 外部取消呼叫，直接往外拋出即可 ----------
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "呼叫中央同步下載 API 時發生例外。");
            return null;
        }
    }
}
