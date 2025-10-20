using System;

namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 編輯技師資料的回應模型。
/// </summary>
public class EditTechnicianResponse
{
    /// <summary>
    /// 技師唯一識別碼。
    /// </summary>
    public string TechnicianUid { get; set; } = string.Empty;

    /// <summary>
    /// 技師姓名。
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// 技師職稱。
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// 所屬門市識別碼。
    /// </summary>
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 最後更新時間 (UTC)。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已更新技師資料。";
}
