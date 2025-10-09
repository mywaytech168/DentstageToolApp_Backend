using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Brand;

/// <summary>
/// 品牌查詢服務例外，封裝狀態碼以利控制器組裝錯誤回應。
/// </summary>
public class BrandQueryServiceException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，指定狀態碼與錯誤訊息。
    /// </summary>
    public BrandQueryServiceException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
