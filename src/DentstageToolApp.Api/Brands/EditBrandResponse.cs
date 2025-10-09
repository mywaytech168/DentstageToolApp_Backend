using System;

namespace DentstageToolApp.Api.Brands;

/// <summary>
/// 編輯品牌的回應模型，回傳更新後的品牌資訊。
/// </summary>
public class EditBrandResponse
{
    /// <summary>
    /// 品牌唯一識別碼。
    /// </summary>
    public string BrandUid { get; set; } = string.Empty;

    /// <summary>
    /// 更新後的品牌名稱。
    /// </summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// 更新時間戳記，回傳 UTC 時間方便前端顯示。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已更新品牌資料。";
}
