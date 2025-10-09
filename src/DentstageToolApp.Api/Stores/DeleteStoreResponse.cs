namespace DentstageToolApp.Api.Stores;

/// <summary>
/// 刪除門市的回應模型。
/// </summary>
public class DeleteStoreResponse
{
    /// <summary>
    /// 已刪除的門市識別碼。
    /// </summary>
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已刪除門市資料。";
}
