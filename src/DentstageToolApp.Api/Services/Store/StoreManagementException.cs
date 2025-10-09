using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市維運服務的自訂例外，用於帶出 HTTP 狀態碼。
/// </summary>
public class StoreManagementException : Exception
{
    /// <summary>
    /// 錯誤對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，建立帶有狀態碼的例外物件。
    /// </summary>
    public StoreManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
