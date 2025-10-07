using System;
using System.Net;

namespace DentstageToolApp.Api.Services.MaintenanceOrder;

/// <summary>
/// 維修單操作發生異常時所拋出的自訂例外，方便控制回傳狀態碼。
/// </summary>
public class MaintenanceOrderManagementException : Exception
{
    /// <summary>
    /// 建構子，帶入 HTTP 狀態碼與錯誤訊息。
    /// </summary>
    public MaintenanceOrderManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 需回應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}
