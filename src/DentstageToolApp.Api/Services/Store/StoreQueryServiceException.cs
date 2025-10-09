using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市查詢服務例外類別。
/// </summary>
public class StoreQueryServiceException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，設定狀態碼與訊息。
    /// </summary>
    public StoreQueryServiceException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
