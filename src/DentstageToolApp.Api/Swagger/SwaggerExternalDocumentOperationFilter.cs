using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DentstageToolApp.Api.Swagger;

/// <summary>
/// 依據 OperationId 自動為 Swagger 加上外部文件連結，方便跳轉至 API 詳細說明頁面。
/// </summary>
public class SwaggerExternalDocumentOperationFilter : IOperationFilter
{
    private readonly string _docsBaseUrl;

    /// <summary>
    /// 建構子，接收外部文件的基底網址，支援相對與絕對路徑設定。
    /// </summary>
    /// <param name="docsBaseUrl">外部文件基底網址或路徑。</param>
    public SwaggerExternalDocumentOperationFilter(string docsBaseUrl)
    {
        // 若組態未指定則回退到 /docs/api，確保預設可用。
        _docsBaseUrl = string.IsNullOrWhiteSpace(docsBaseUrl)
            ? "/docs/api"
            : docsBaseUrl;
    }

    /// <summary>
    /// 在 Swagger Operation 物件上填入外部文件資訊，建立跳轉按鈕。
    /// </summary>
    /// <param name="operation">目前處理中的 Swagger Operation。</param>
    /// <param name="context">提供 Operation 與 Action 相關資訊的內容物件。</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation is null || string.IsNullOrWhiteSpace(operation.OperationId))
        {
            // 沒有 OperationId 無法組出連結，因此直接跳過。
            return;
        }

        // 若組態僅提供資料夾路徑，則補上預設的 index.html，維持清楚的入口頁。
        var baseUrl = _docsBaseUrl.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? _docsBaseUrl
            : $"{_docsBaseUrl.TrimEnd('/')}/index.html";

        // 為避免既有查詢參數遭覆寫，改用適當的分隔符號拼接 operationId。
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var documentUrl = $"{baseUrl}{separator}operationId={Uri.EscapeDataString(operation.OperationId)}";

        // 實際將外部文件資訊掛載到 Swagger，UI 會自動呈現跳轉按鈕。
        operation.ExternalDocs = new OpenApiExternalDocs
        {
            Description = "查看操作教學與範例",
            Url = new Uri(documentUrl, UriKind.RelativeOrAbsolute)
        };
    }
}
