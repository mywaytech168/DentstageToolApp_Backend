using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 門市列表回應物件。
/// </summary>
public class StoreListResponse
{
    /// <summary>
    /// 門市資料集合。
    /// </summary>
    public List<StoreListItem> Items { get; set; } = new();

    /// <summary>
    /// 分頁資訊，協助前端掌握目前頁碼與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();
}

/// <summary>
/// 門市列表單筆資料。
/// </summary>
public class StoreListItem
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
