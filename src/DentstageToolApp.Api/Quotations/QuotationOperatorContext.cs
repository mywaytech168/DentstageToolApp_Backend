namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 建立估價單時的操作人員資訊，來源為 JWT 權杖的使用者資訊。
/// </summary>
public class QuotationOperatorContext
{
    /// <summary>
    /// 操作人員唯一識別碼，用於回填報價單的 UserUid 欄位。
    /// </summary>
    public string? UserUid { get; set; }

    /// <summary>
    /// 操作人員顯示名稱，會同步寫入建立與修改者欄位。
    /// </summary>
    public string OperatorName { get; set; } = string.Empty;
}
