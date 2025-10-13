namespace DentstageToolApp.Api.Models.Brands;

/// <summary>
/// 刪除品牌的回應模型，回傳刪除結果訊息。
/// </summary>
public class DeleteBrandResponse
{
    /// <summary>
    /// 已刪除的品牌識別碼。
    /// </summary>
    public string BrandUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息，提示刪除完成。
    /// </summary>
    public string Message { get; set; } = "已刪除品牌資料。";
}
