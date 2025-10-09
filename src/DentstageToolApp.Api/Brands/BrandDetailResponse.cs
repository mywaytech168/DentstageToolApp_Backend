namespace DentstageToolApp.Api.Brands;

/// <summary>
/// 品牌詳細資料回應物件。
/// </summary>
public class BrandDetailResponse
{
    /// <summary>
    /// 品牌識別碼。
    /// </summary>
    public string BrandUid { get; set; } = string.Empty;

    /// <summary>
    /// 品牌名稱。
    /// </summary>
    public string BrandName { get; set; } = string.Empty;
}
