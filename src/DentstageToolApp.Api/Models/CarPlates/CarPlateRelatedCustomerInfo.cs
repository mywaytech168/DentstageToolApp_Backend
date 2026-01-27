namespace DentstageToolApp.Api.Models.CarPlates;

/// <summary>
/// 車牌搜尋時回傳的客戶資訊，自動提取自工單或估價單。
/// </summary>
public class CarPlateRelatedCustomerInfo
{
    /// <summary>
    /// 客戶唯一識別碼（CustomerUID）。
    /// </summary>
    public string? CustomerUid { get; set; }

    /// <summary>
    /// 客戶姓名。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 客戶類型（例如個人、公司、臨時客）。
    /// </summary>
    public string? CustomerType { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 電子郵件。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 所在縣市。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 所在鄉鎮市區。
    /// </summary>
    public string? Township { get; set; }

    /// <summary>
    /// 客戶備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 消息來源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 詢問原因。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 是否為臨時客戶。
    /// </summary>
    public bool IsTemporaryCustomer { get; set; }
}
