using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Brands;

/// <summary>
/// 品牌列表回應物件，提供品牌清單資料。
/// </summary>
public class BrandListResponse
{
    /// <summary>
    /// 品牌資料集合。
    /// </summary>
    public List<BrandListItem> Items { get; set; } = new();
}

/// <summary>
/// 品牌列表單筆資料。
/// </summary>
public class BrandListItem
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
