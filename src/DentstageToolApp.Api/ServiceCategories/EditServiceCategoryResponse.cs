using System;

namespace DentstageToolApp.Api.ServiceCategories;

/// <summary>
/// 編輯服務類別的回應模型。
/// </summary>
public class EditServiceCategoryResponse
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
    /// 更新時間戳記。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已更新服務類別資料。";
}
