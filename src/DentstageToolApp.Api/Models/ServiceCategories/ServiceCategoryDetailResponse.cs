namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 服務類別詳細資料回應物件。
/// </summary>
public class ServiceCategoryDetailResponse
{
    /// <summary>
    /// 維修類型中文標籤。
    /// </summary>
    public string FixType { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別顯示名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
}
