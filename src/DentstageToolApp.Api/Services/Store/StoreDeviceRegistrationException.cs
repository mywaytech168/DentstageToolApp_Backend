using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市裝置註冊例外狀況，用於回傳對應的 HTTP 錯誤碼與訊息。
/// </summary>
public class StoreDeviceRegistrationException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼，協助控制器輸出正確的錯誤回應。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構例外物件並指定狀態碼與訊息。
    /// </summary>
    public StoreDeviceRegistrationException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
