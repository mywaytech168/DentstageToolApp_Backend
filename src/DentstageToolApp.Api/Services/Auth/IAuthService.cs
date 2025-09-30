using DentstageToolApp.Api.Auth;

namespace DentstageToolApp.Api.Services.Auth;

/// <summary>
/// 定義身份驗證流程所需的服務介面。
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 執行登入流程並回傳 Token 與使用者資訊。
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 透過 Refresh Token 重新取得新的存取權杖。
    /// </summary>
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 依使用者識別碼取得登入者的顯示名稱與角色資訊。
    /// </summary>
    Task<AuthInfoResponse> GetUserInfoAsync(string userUid, CancellationToken cancellationToken);
}
