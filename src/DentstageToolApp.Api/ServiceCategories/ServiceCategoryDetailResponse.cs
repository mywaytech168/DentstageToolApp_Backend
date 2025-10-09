namespace DentstageToolApp.Api.ServiceCategories;

/// <summary>
/// 服務類別詳細資料回應物件。
/// </summary>
public class ServiceCategoryDetailResponse
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
