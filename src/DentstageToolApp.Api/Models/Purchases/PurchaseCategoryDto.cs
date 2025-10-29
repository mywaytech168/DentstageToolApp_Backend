namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購品項類別資料傳輸物件。
/// </summary>
public class PurchaseCategoryDto
{
    /// <summary>
    /// 類別識別碼。
    /// </summary>
    public string CategoryUid { get; set; } = string.Empty;

    /// <summary>
    /// 類別名稱。
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
}
