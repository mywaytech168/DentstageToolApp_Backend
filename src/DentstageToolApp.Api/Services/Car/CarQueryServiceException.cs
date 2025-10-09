using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛查詢服務自訂例外類別，封裝 HTTP 狀態碼以利控制器轉換錯誤回應。
/// </summary>
public class CarQueryServiceException : Exception
{
    /// <summary>
    /// 例外對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，指定錯誤訊息與對應狀態碼。
    /// </summary>
    public CarQueryServiceException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
