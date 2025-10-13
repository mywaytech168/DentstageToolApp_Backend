using System;

namespace DentstageToolApp.Api.Models.VehicleModels;

/// <summary>
/// 編輯車型的回應模型。
/// </summary>
public class EditModelResponse
{
    /// <summary>
    /// 車型唯一識別碼。
    /// </summary>
    public string ModelUid { get; set; } = string.Empty;

    /// <summary>
    /// 車型名稱。
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 對應的品牌識別碼。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 更新時間戳記。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已更新車型資料。";
}
