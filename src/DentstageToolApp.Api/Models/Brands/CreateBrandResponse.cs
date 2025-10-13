namespace DentstageToolApp.Api.Models.Brands;

/// <summary>
/// 建立品牌的回應模型，回傳新品牌的識別資訊。
/// </summary>
public class CreateBrandResponse
{
    /// <summary>
    /// 品牌唯一識別碼，方便前端後續維護使用。
    /// </summary>
    public string BrandUid { get; set; } = string.Empty;

    /// <summary>
    /// 品牌名稱。
    /// </summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息，提示建立已完成。
    /// </summary>
    public string Message { get; set; } = "已建立品牌資料。";
}
