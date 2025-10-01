using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶維運相關操作失敗時所拋出的自訂例外，方便轉換對應的 HTTP 狀態碼。
/// </summary>
public class CustomerManagementException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建立例外並指派狀態碼與訊息。
    /// </summary>
    public CustomerManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
