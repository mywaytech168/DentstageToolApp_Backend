using System.Diagnostics.CodeAnalysis;

namespace DentstageToolApp.Api.Models.Auth;

/// <summary>
/// 提供登入者查詢個人資訊時所需的基本資料模型。
/// </summary>
public class AuthInfoResponse
{
    /// <summary>
    /// 使用者顯示名稱，對應 useraccounts.DisplayName 欄位。
    /// </summary>
    [AllowNull]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 使用者角色字串，對應 useraccounts.Role 欄位。
    /// </summary>
    [AllowNull]
    public string? Role { get; set; }
}
