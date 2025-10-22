using System.Collections.Generic;
using DentstageToolApp.Api.Models.BrandModels;
using DentstageToolApp.Api.Models.Pagination;

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

    /// <summary>
    /// 分頁資訊，協助前端掌握總頁數與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();
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

    /// <summary>
    /// 品牌底下可選用的車型清單，方便前端直接渲染下拉選項。
    /// </summary>
    public List<BrandModelOption> Models { get; set; } = new();
}
