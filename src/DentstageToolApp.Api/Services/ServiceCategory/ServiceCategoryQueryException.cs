using System;
using System.Net;

namespace DentstageToolApp.Api.Services.ServiceCategory;

/// <summary>
/// 服務類別查詢服務例外，提供狀態碼資訊。
/// </summary>
public class ServiceCategoryQueryException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，設定狀態碼與訊息。
    /// </summary>
    public ServiceCategoryQueryException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
