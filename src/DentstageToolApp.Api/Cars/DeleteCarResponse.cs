namespace DentstageToolApp.Api.Cars;

/// <summary>
/// 刪除車輛資料的回應模型。
/// </summary>
public class DeleteCarResponse
{
    /// <summary>
    /// 已刪除的車輛識別碼。
    /// </summary>
    public string CarUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已刪除車輛資料。";
}
