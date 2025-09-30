namespace DentstageToolApp.Api.Permissions;

/// <summary>
/// 權限選單查詢回應，僅回傳使用者角色字串供前端比對。
/// </summary>
public class PermissionRoleResponse
{
    /// <summary>
    /// 使用者角色資訊，來源為 useraccounts.Role。
    /// </summary>
    public string? Role { get; set; }
}
