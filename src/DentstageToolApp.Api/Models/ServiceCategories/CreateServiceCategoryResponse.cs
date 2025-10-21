namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 建立服務類別的回應模型。
/// </summary>
public class CreateServiceCategoryResponse
{
    /// <summary>
    /// 維修類型中文標籤，對應凹痕、美容、板烤或其他。
    /// </summary>
    public string FixType { get; set; } = string.Empty;

    /// <summary>
    /// 服務類別顯示名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已建立服務類別資料。";
}
