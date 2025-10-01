using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶查詢服務使用時發生錯誤的自訂例外，便於控制回應狀態碼。
/// </summary>
public class CustomerLookupException : Exception
{
    /// <summary>
    /// 建立例外時指定的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，設定錯誤訊息與對應的狀態碼。
    /// </summary>
    public CustomerLookupException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
