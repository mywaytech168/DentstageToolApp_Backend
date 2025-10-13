using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.BrandModels;

/// <summary>
/// 品牌與型號清單回應物件，提供前端建置下拉選單使用。
/// </summary>
public class BrandModelListResponse
{
    /// <summary>
    /// 品牌與其所屬型號清單集合。
    /// </summary>
    public List<BrandModelItem> Items { get; set; } = new();
}

/// <summary>
/// 品牌資料與底下可選型號清單。
/// </summary>
public class BrandModelItem
{
    /// <summary>
    /// 品牌識別碼（UID）。
    /// </summary>
    public string BrandUid { get; set; } = string.Empty;

    /// <summary>
    /// 品牌名稱。
    /// </summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// 該品牌底下可使用的車型選項。
    /// </summary>
    public List<BrandModelOption> Models { get; set; } = new();
}

/// <summary>
/// 車型資料，對應單一品牌底下的具體車款。
/// </summary>
public class BrandModelOption
{
    /// <summary>
    /// 車型識別碼（UID）。
    /// </summary>
    public string ModelUid { get; set; } = string.Empty;

    /// <summary>
    /// 車型名稱。
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
}
