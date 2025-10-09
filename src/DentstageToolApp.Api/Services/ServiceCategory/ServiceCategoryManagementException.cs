using System;
using System.Net;

namespace DentstageToolApp.Api.Services.ServiceCategory;

/// <summary>
/// 服務類別維運服務的自訂例外，用於攜帶狀態碼。
/// </summary>
public class ServiceCategoryManagementException : Exception
{
    /// <summary>
    /// 例外對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，建立帶有狀態碼的例外。
    /// </summary>
    public ServiceCategoryManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
