namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 刪除技師資料的回應模型。
/// </summary>
public class DeleteTechnicianResponse
{
    /// <summary>
    /// 技師唯一識別碼。
    /// </summary>
    public string TechnicianUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已刪除技師資料。";
}
