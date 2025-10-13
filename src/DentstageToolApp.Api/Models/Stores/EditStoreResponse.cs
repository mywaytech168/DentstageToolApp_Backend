using System;

namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 編輯門市的回應模型。
/// </summary>
public class EditStoreResponse
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
    /// 更新時間戳記。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已更新門市資料。";
}
