using System;

namespace DentstageToolApp.Api.Models.Sync;

/// <summary>
/// 定義同步傳輸模式常數與工具方法，方便切換 Http 與 RabbitMQ。
/// </summary>
public static class SyncTransportModes
{
    /// <summary>
    /// 透過 REST API 進行同步。
    /// </summary>
    public const string Http = "Http";

    /// <summary>
    /// 使用 RabbitMQ RPC Pattern 傳遞同步訊息。
    /// </summary>
    public const string RabbitMq = "RabbitMq";

    /// <summary>
    /// 正規化輸入字串成既定常數名稱。
    /// </summary>
    public static string Normalize(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return Http;
        }

        if (string.Equals(mode, RabbitMq, StringComparison.OrdinalIgnoreCase))
        {
            return RabbitMq;
        }

        return Http;
    }
}
