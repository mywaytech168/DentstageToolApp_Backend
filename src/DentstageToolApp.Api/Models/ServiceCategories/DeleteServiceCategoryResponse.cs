namespace DentstageToolApp.Api.Models.ServiceCategories;

/// <summary>
/// 刪除服務類別的回應模型。
/// </summary>
public class DeleteServiceCategoryResponse
{
    /// <summary>
    /// 已刪除的服務類別識別碼。
    /// </summary>
    public string ServiceCategoryUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已刪除服務類別資料。";
}
