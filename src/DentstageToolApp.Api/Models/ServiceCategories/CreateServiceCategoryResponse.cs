namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 建立服務類別的回應模型。
/// </summary>
public class CreateServiceCategoryResponse
{
    /// <summary>
    /// 服務類別唯一識別碼。
    /// </summary>
    public string ServiceCategoryUid { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已建立服務類別資料。";
}
