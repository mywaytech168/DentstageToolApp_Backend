using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Auth;
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
    private const string DownloadEndpoint = "/api/sync/change";
    private const string LoginEndpoint = "/api/auth/login";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<RemoteSyncApiClient> _logger;
    private readonly SyncOptions _syncOptions;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

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
    public async Task<SyncUploadResult?> UploadChangeAsync(SyncUploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return await SendWithAuthorizationAsync(
            async token =>
            {
                // ---------- 每次呼叫建立獨立請求並帶入最新權杖 ----------
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint)
                {
                    Content = JsonContent.Create(request, options: SerializerOptions)
                };
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                return await _httpClient.SendAsync(requestMessage, cancellationToken);
            },
            async response =>
            {
                return await response.Content.ReadFromJsonAsync<SyncUploadResult>(SerializerOptions, cancellationToken);
            },
            cancellationToken);
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

        return await SendWithAuthorizationAsync(
            async token =>
            {
                var parameters = new Dictionary<string, string?>();
                if (query.LastSyncTime.HasValue)
                {
                    // ---------- 僅在有提供最後同步時間時加入查詢參數 ----------
                    parameters["lastSyncTime"] = query.LastSyncTime.Value.ToString("O");
                }

                var requestUri = parameters.Count > 0
                    ? QueryHelpers.AddQueryString(DownloadEndpoint, parameters)
                    : DownloadEndpoint;
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                return await _httpClient.SendAsync(requestMessage, cancellationToken);
            },
            async response =>
            {
                return await response.Content.ReadFromJsonAsync<SyncDownloadResponse>(SerializerOptions, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// 於發送 API 前確保擁有有效權杖，若遇到未授權會自動重新登入一次。
    /// </summary>
    private async Task<T?> SendWithAuthorizationAsync<T>(
        Func<string, Task<HttpResponseMessage>> sendAsync,
        Func<HttpResponseMessage, Task<T?>> parseAsync,
        CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await EnsureAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var response = await sendAsync(token);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // ---------- 權杖失效時先釋放回應，再嘗試重新登入 ----------
                    response.Dispose();
                    _logger.LogWarning("中央 API 回應未授權，將清除 Token 並嘗試重新登入。");
                    _syncOptions.ClearAuthTokens();
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "呼叫中央同步 API 失敗，狀態碼: {StatusCode}, 訊息: {Message}",
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(message) ? "<空白>" : message);
                    response.Dispose();
                    return null;
                }

                using (response)
                {
                    return await parseAsync(response);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "呼叫中央同步 API 時發生例外。");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// 確保目前擁有有效 Access Token，必要時會透過機碼重新登入中央 API。
    /// </summary>
    private async Task<string?> EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_syncOptions.HasValidAccessToken())
        {
            return _syncOptions.GetAccessToken();
        }

        if (string.IsNullOrWhiteSpace(_syncOptions.MachineKey))
        {
            _logger.LogWarning("未設定同步機碼，無法向中央取得授權 Token。");
            return null;
        }

        await _tokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_syncOptions.HasValidAccessToken())
            {
                return _syncOptions.GetAccessToken();
            }

            var loginRequest = new LoginRequest
            {
                DeviceKey = _syncOptions.MachineKey
            };

            using var response = await _httpClient.PostAsJsonAsync(LoginEndpoint, loginRequest, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "透過同步機碼登入中央失敗，狀態碼: {StatusCode}, 訊息: {Message}",
                    response.StatusCode,
                    string.IsNullOrWhiteSpace(message) ? "<空白>" : message);
                _syncOptions.ClearAuthTokens();
                return null;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(SerializerOptions, cancellationToken);
            if (loginResponse is null || string.IsNullOrWhiteSpace(loginResponse.AccessToken))
            {
                _logger.LogError("中央登入 API 回傳內容缺少 Access Token，無法進行同步。");
                _syncOptions.ClearAuthTokens();
                return null;
            }

            _syncOptions.ApplyLoginResponse(loginResponse);
            return _syncOptions.GetAccessToken();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得同步授權 Token 時發生例外。");
            _syncOptions.ClearAuthTokens();
            return null;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }
}
