using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Model;

/// <summary>
/// 車型維運服務的自訂例外，用於回傳對應的 HTTP 狀態碼。
/// </summary>
public class ModelManagementException : Exception
{
    /// <summary>
    /// 發生錯誤時對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，指派狀態碼與訊息。
    /// </summary>
    public ModelManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
