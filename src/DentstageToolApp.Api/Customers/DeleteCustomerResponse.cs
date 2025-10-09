namespace DentstageToolApp.Api.Customers;

/// <summary>
/// 刪除客戶資料的回應模型。
/// </summary>
public class DeleteCustomerResponse
{
    /// <summary>
    /// 已刪除的客戶識別碼。
    /// </summary>
    public string CustomerUid { get; set; } = string.Empty;

    /// <summary>
    /// 操作結果訊息。
    /// </summary>
    public string Message { get; set; } = "已刪除客戶資料。";
}
