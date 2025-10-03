using System;
using System.Linq;
using System.Text;
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
        if (operation is null)
        {
            // Operation 為 null 時無法操作屬性，直接忽略避免例外。
            return;
        }

        // 若 Swagger 規格缺少 OperationId，改以 HTTP 方法與路徑組成備援識別碼。
        var operationId = string.IsNullOrWhiteSpace(operation.OperationId)
            ? BuildFallbackOperationId(context)
            : operation.OperationId;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            // 即便嘗試生成仍失敗時，仍然不要強行建立外部連結以免產生錯誤網址。
            return;
        }

        // 將運算後的 OperationId 回寫至 Swagger，確保前端與 Swagger UI 皆能共用同一識別碼。
        operation.OperationId ??= operationId;

        // 若組態僅提供資料夾路徑，則補上預設的 index.html，維持清楚的入口頁。
        var baseUrl = _docsBaseUrl.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? _docsBaseUrl
            : $"{_docsBaseUrl.TrimEnd('/')}/index.html";

        // 為避免既有查詢參數遭覆寫，改用適當的分隔符號拼接 operationId。
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var documentUrl = $"{baseUrl}{separator}operationId={Uri.EscapeDataString(operationId)}";

        // 實際將外部文件資訊掛載到 Swagger，UI 會自動呈現跳轉按鈕。
        operation.ExternalDocs = new OpenApiExternalDocs
        {
            Description = "查看操作教學與範例",
            Url = new Uri(documentUrl, UriKind.RelativeOrAbsolute)
        };
    }

    /// <summary>
    /// 針對缺漏 OperationId 的情境建立備援識別碼，以 HTTP 方法搭配路徑片段生成可讀文字。
    /// </summary>
    /// <param name="context">Swagger 運算提供的描述內容，內含路徑與方法資訊。</param>
    /// <returns>回傳可用的識別碼，若無法生成則回傳 <c>null</c>。</returns>
    private static string? BuildFallbackOperationId(OperationFilterContext context)
    {
        if (context?.ApiDescription is null)
        {
            // 無法取得 ApiDescription 時就無法推導路徑與方法，直接返回 null。
            return null;
        }

        var httpMethod = context.ApiDescription.HttpMethod;
        var relativePath = context.ApiDescription.RelativePath;
        if (string.IsNullOrWhiteSpace(httpMethod) || string.IsNullOrWhiteSpace(relativePath))
        {
            // 缺少必要資訊時無從組合識別碼，維持 null 以利上層判斷。
            return null;
        }

        // 移除查詢字串並將路徑參數大括號去除，避免識別碼出現不必要符號。
        var normalizedPath = relativePath.Split('?', '#')[0]
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal);

        var builder = new StringBuilder();
        foreach (var character in normalizedPath)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                continue;
            }

            // 其他符號一律轉為底線，保持識別碼可讀性與一致性。
            builder.Append('_');
        }

        var sanitizedPath = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(sanitizedPath))
        {
            return null;
        }

        // 以 HTTP 方法作為前綴，避免不同路徑卻相同尾碼時發生衝突。
        var fallbackId = string.Join('_', new[]
        {
            httpMethod.ToUpperInvariant(),
            sanitizedPath
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(fallbackId) ? null : fallbackId;
    }
}
