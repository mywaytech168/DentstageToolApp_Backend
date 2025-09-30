using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Admin;

/// <summary>
/// 管理者操作失敗時拋出的例外，攜帶對應的 HTTP 狀態碼。
/// </summary>
public class AccountAdminException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼，便於控制器轉換為 ProblemDetails。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 以訊息與狀態碼建立例外執行個體。
    /// </summary>
    public AccountAdminException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
