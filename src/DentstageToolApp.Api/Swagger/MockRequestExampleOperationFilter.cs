using System;
using System.Linq;
using DentstageToolApp.Api.Swagger;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DentstageToolApp.Api.Swagger;

/// <summary>
/// 讀取 <see cref="SwaggerMockRequestExampleAttribute"/>，將 Mock 範例資料套用到 Swagger Request Body。
/// </summary>
public class MockRequestExampleOperationFilter : IOperationFilter
{
    /// <summary>
    /// 將 Attribute 定義的範例寫入 Swagger Operation，提供開發與測試人員直接貼上測試資料。
    /// </summary>
    /// <param name="operation">當前操作定義。</param>
    /// <param name="context">Swagger 產生時的描述內容。</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody?.Content is null)
        {
            // 沒有 Request Body 的 API 無須處理，直接結束。
            return;
        }

        var exampleAttributes = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<SwaggerMockRequestExampleAttribute>()
            .ToList();

        if (!exampleAttributes.Any())
        {
            return;
        }

        foreach (var attribute in exampleAttributes)
        {
            // 確保內容型別存在，若 Swagger 預設未建立則主動補上。
            if (!operation.RequestBody.Content.TryGetValue(attribute.ContentType, out var mediaType))
            {
                mediaType = new OpenApiMediaType();
                operation.RequestBody.Content[attribute.ContentType] = mediaType;
            }

            mediaType.Example = BuildExample(attribute.ExampleJson);
        }
    }

    /// <summary>
    /// 依範例字串建立對應的 OpenApi 資料結構，若解析失敗則以字串呈現避免整體失敗。
    /// </summary>
    /// <param name="exampleJson">Mock 範例字串。</param>
    /// <returns>可被 Swagger UI 呈現的 Example。</returns>
    private static IOpenApiAny BuildExample(string exampleJson)
    {
        if (string.IsNullOrWhiteSpace(exampleJson))
        {
            return new OpenApiString(string.Empty);
        }

        try
        {
            return OpenApiAnyFactory.CreateFromJson(exampleJson);
        }
        catch (Exception)
        {
            // 例外時退回純文字，仍能提示開發者範例內容。
            return new OpenApiString(exampleJson);
        }
    }
}
