using System.Collections.Generic;

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
}

/// <summary>
/// 服務類別列表單筆資料。
/// </summary>
public class ServiceCategoryListItem
{
    /// <summary>
    /// 服務類別識別碼。
    /// </summary>
    public string ServiceCategoryUid { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
}
