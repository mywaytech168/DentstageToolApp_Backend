using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Purchase;

/// <summary>
/// 採購服務專用例外，用於攜帶對應的 HTTP 狀態碼與訊息。
/// </summary>
public class PurchaseServiceException : Exception
{
    /// <summary>
    /// 建構子，指定狀態碼與錯誤訊息。
    /// </summary>
    public PurchaseServiceException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 錯誤對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}
