using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Auth;

/// <summary>
/// 自訂身份驗證例外狀況，攜帶對應的 HTTP 狀態碼。
/// </summary>
public class AuthException : Exception
{
    /// <summary>
    /// 建立例外狀況並指定狀態碼與訊息。
    /// </summary>
    public AuthException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}
