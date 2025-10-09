namespace DentstageToolApp.Api.Models;

/// <summary>
/// 建立車型的回應模型，回傳新建立的車型資訊。
/// </summary>
public class CreateModelResponse
{
    /// <summary>
    /// 車型唯一識別碼。
    /// </summary>
    public string ModelUid { get; set; } = string.Empty;

    /// <summary>
    /// 車型名稱。
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 車型所屬品牌識別碼，若為 null 表示尚未綁定品牌。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已建立車型資料。";
}
