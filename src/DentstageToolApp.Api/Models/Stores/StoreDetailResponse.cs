namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 門市詳細資料回應物件。
/// </summary>
public class StoreDetailResponse
{
    /// <summary>
    /// 門市識別碼。
    /// </summary>
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 門市名稱。
    /// </summary>
    public string StoreName { get; set; } = string.Empty;
}
