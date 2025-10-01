using System;
using System.Net;

namespace DentstageToolApp.Api.Services.Quotation;

/// <summary>
/// 估價單維運相關操作失敗時拋出的自訂例外，方便控制回傳狀態碼。
/// </summary>
public class QuotationManagementException : Exception
{
    /// <summary>
    /// 對應的 HTTP 狀態碼。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 建立例外並指定狀態碼與錯誤訊息。
    /// </summary>
    public QuotationManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

