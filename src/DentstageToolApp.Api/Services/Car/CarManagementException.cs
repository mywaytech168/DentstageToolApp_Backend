using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛維運相關操作失敗時所拋出的自訂例外，方便對應 HTTP 狀態碼。
/// </summary>
public class CarManagementException : Exception
{
    /// <summary>
    /// 失敗時對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建立例外並指派狀態碼與錯誤訊息。
    /// </summary>
    public CarManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
