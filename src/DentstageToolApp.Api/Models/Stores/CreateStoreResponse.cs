namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 建立門市的回應模型。
/// </summary>
public class CreateStoreResponse
{
    /// <summary>
    /// 門市唯一識別碼。
    /// </summary>
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 門市名稱。
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已建立門市資料。";
}
