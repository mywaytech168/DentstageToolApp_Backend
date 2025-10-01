using System;

namespace DentstageToolApp.Api.Swagger;

/// <summary>
/// 標註在控制器方法上，為 Swagger Request Body 提供可直接貼上的 Mock 範例資料。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SwaggerMockRequestExampleAttribute : Attribute
{
    /// <summary>
    /// 建立範例屬性，預設使用 JSON 內容型別。
    /// </summary>
    /// <param name="exampleJson">符合請求格式的範例字串，建議使用 Raw String 直接撰寫 JSON。</param>
    /// <param name="contentType">Swagger 要套用的內容型別，預設為 application/json。</param>
    public SwaggerMockRequestExampleAttribute(string exampleJson, string contentType = "application/json")
    {
        ExampleJson = exampleJson;
        ContentType = contentType;
    }

    /// <summary>
    /// Swagger 要顯示的範例 JSON 字串。
    /// </summary>
    public string ExampleJson { get; }

    /// <summary>
    /// 對應的內容型別，預設為 application/json。
    /// </summary>
    public string ContentType { get; }
}
