namespace DentstageToolApp.Api.Models;

/// <summary>
/// 刪除車型的回應模型。
/// </summary>
public class DeleteModelResponse
{
    /// <summary>
    /// 已刪除的車型識別碼。
    /// </summary>
    public string ModelUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已刪除車型資料。";
}
