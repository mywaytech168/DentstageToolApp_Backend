namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 建立技師資料的回應模型。
/// </summary>
public class CreateTechnicianResponse
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
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已建立技師資料。";
}
