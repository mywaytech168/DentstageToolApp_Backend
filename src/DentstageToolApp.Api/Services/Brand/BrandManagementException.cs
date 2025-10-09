using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Brand;

/// <summary>
/// 品牌維運服務的自訂例外，用於攜帶 HTTP 狀態碼與錯誤訊息。
/// </summary>
public class BrandManagementException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建構子，用於建立帶有狀態碼的例外物件。
    /// </summary>
    public BrandManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
