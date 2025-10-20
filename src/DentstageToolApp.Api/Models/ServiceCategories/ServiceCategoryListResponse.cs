using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 服務類別列表回應物件。
/// </summary>
public class ServiceCategoryListResponse
{
    /// <summary>
    /// 服務類別資料集合。
    /// </summary>
    public List<ServiceCategoryListItem> Items { get; set; } = new();

    /// <summary>
    /// 分頁資訊，提供前端計算下一頁與總頁數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();
}

/// <summary>
/// 服務類別列表單筆資料。
/// </summary>
public class ServiceCategoryListItem
{
    /// <summary>
    /// 維修類型鍵值。
    /// </summary>
    public string FixType { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別顯示名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
}
