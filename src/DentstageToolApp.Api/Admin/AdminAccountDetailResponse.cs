using System.Diagnostics.CodeAnalysis;

namespace DentstageToolApp.Api.Admin;

/// <summary>
/// 管理者查詢帳號時的精簡回應模型，只揭露前端需要的基本資訊。
/// </summary>
public class AdminAccountDetailResponse
{
    /// <summary>
    /// 使用者顯示名稱，對應 useraccounts.DisplayName 欄位。
    /// </summary>
    [AllowNull]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 使用者角色字串，直接對應 useraccounts.Role 欄位。
    /// </summary>
    [AllowNull]
    public string? Role { get; set; }
}
