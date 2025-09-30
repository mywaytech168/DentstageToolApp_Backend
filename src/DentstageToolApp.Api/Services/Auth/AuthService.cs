using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DentstageToolApp.Api.Auth;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DentstageToolApp.Api.Services.Auth;

/// <summary>
/// 身份驗證服務實作，專責處理裝置機碼驗證與 Token 發放。
/// </summary>
public class AuthService : IAuthService
{
    private readonly DentstageToolAppContext _context;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// 預先快取 JWT Token 產生器，減少重複建立的成本。
    /// </summary>
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    /// <summary>
    /// 建構子，注入資料庫內容、JWT 設定與記錄器。
    /// </summary>
    public AuthService(
        DentstageToolAppContext context,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // 以裝置機碼進行查詢，並載入使用者與既有 Token 資料
        var device = await _context.DeviceRegistrations
            .Include(x => x.UserAccount)
            .Include(x => x.RefreshTokens)
            .FirstOrDefaultAsync(x => x.DeviceKey == request.DeviceKey, cancellationToken);

        if (device is null)
        {
            throw new AuthException(HttpStatusCode.Unauthorized, "找不到對應裝置，請確認機碼是否正確。");
        }

        var user = device.UserAccount;
        if (user is null)
        {
            throw new AuthException(HttpStatusCode.InternalServerError, "裝置缺少對應的使用者資訊，請聯絡管理員。");
        }

        if (!user.IsActive)
        {
            throw new AuthException(HttpStatusCode.Forbidden, "帳號已被停用，請聯絡管理員。");
        }

        if (device.IsBlackListed || !string.Equals(device.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException(HttpStatusCode.Forbidden, "此裝置已被停用或封鎖，請洽管理員處理。");
        }

        if (device.ExpireAt.HasValue && device.ExpireAt.Value < DateTime.UtcNow)
        {
            throw new AuthException(HttpStatusCode.Forbidden, "裝置授權已過期，請聯絡管理員重新啟用。");
        }

        // 清理既有的過期 Refresh Token，避免資料持續累積
        await CleanupExpiredTokensAsync(device.DeviceRegistrationUid, cancellationToken);

        var now = DateTime.UtcNow;
        var accessTokenExpireAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var refreshTokenExpireAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        var accessToken = GenerateAccessToken(user, device, now, accessTokenExpireAt);
        var refreshToken = GenerateRefreshToken(user, device, refreshTokenExpireAt, now);

        user.LastLoginAt = now;
        device.LastSignInAt = now;
        if (!device.ExpireAt.HasValue || device.ExpireAt.Value < refreshTokenExpireAt)
        {
            device.ExpireAt = refreshTokenExpireAt;
        }
        device.ModificationTimestamp = now;
        device.ModifiedBy = GetUserIdentityLabel(user);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("使用者 {Account} 的裝置 {Device} 成功登入。", GetUserIdentityLabel(user), device.DeviceRegistrationUid);

        return new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpireAt = accessTokenExpireAt,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpireAt = refreshTokenExpireAt,
            DisplayName = user.DisplayName,
            Role = user.Role,
            DeviceStatus = device.Status,
            Message = "登入成功，已發放新的權杖。"
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        // 先以 Token 字串尋找資料，並載入關聯資訊
        var refreshToken = await _context.RefreshTokens
            .Include(x => x.UserAccount)
            .Include(x => x.DeviceRegistration)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null)
        {
            throw new AuthException(HttpStatusCode.Unauthorized, "Refresh Token 無效或已被撤銷。");
        }

        if (refreshToken.IsRevoked)
        {
            throw new AuthException(HttpStatusCode.Unauthorized, "Refresh Token 已被撤銷，請重新登入。");
        }

        if (refreshToken.ExpireAt < DateTime.UtcNow)
        {
            // 將過期 Token 標記為撤銷，避免重複查詢
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            throw new AuthException(HttpStatusCode.Unauthorized, "Refresh Token 已過期，請重新登入。");
        }

        if (refreshToken.DeviceRegistration is null || refreshToken.DeviceRegistration.DeviceKey != request.DeviceKey)
        {
            throw new AuthException(HttpStatusCode.Unauthorized, "裝置驗證失敗，請重新登入。");
        }

        var user = refreshToken.UserAccount;
        if (!user.IsActive)
        {
            throw new AuthException(HttpStatusCode.Forbidden, "帳號已被停用，請聯絡管理員。");
        }

        var device = refreshToken.DeviceRegistration;
        if (device.IsBlackListed || !string.Equals(device.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException(HttpStatusCode.Forbidden, "此裝置已被停用或封鎖。");
        }

        // 旋轉 Refresh Token：先撤銷舊的，再建立新的 Token 記錄
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;
        var accessTokenExpireAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var refreshTokenExpireAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        var accessToken = GenerateAccessToken(user, device, now, accessTokenExpireAt);
        var newRefreshToken = GenerateRefreshToken(user, device, refreshTokenExpireAt, now);

        device.LastSignInAt = now;
        if (!device.ExpireAt.HasValue || device.ExpireAt.Value < refreshTokenExpireAt)
        {
            device.ExpireAt = refreshTokenExpireAt;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("使用者 {Account} 透過裝置 {Device} 更新 Token。", GetUserIdentityLabel(user), device.DeviceRegistrationUid);

        return new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpireAt = accessTokenExpireAt,
            RefreshToken = newRefreshToken.Token,
            RefreshTokenExpireAt = refreshTokenExpireAt,
            DisplayName = user.DisplayName,
            Role = user.Role,
            DeviceStatus = device.Status,
            Message = "已更新權杖。"
        };
    }

    /// <summary>
    /// 產生 Access Token，並設定必要的 Claims。
    /// </summary>
    private string GenerateAccessToken(UserAccount user, DeviceRegistration device, DateTime generatedAt, DateTime expireAt)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // 建立 Claims，包含使用者識別與角色資訊
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserUid),
            new(JwtRegisteredClaimNames.UniqueName, user.UserUid),
            new("displayName", user.DisplayName ?? string.Empty),
            new("device", device.DeviceRegistrationUid)
        };

        if (!string.IsNullOrWhiteSpace(user.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, user.Role));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: generatedAt,
            expires: expireAt,
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// 建立新的 Refresh Token 並儲存到資料庫。
    /// </summary>
    private RefreshToken GenerateRefreshToken(UserAccount user, DeviceRegistration device, DateTime expireAt, DateTime now)
    {
        var tokenString = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshToken = new RefreshToken
        {
            RefreshTokenUid = Guid.NewGuid().ToString("N"),
            Token = tokenString,
            UserUid = user.UserUid,
            DeviceRegistrationUid = device.DeviceRegistrationUid,
            ExpireAt = expireAt,
            CreationTimestamp = now,
            CreatedBy = GetUserIdentityLabel(user),
            UserAccount = user,
            DeviceRegistration = device
        };

        _context.RefreshTokens.Add(refreshToken);
        return refreshToken;
    }

    /// <summary>
    /// 取得使用者顯示標籤，優先採用顯示名稱，若無則回退至唯一識別碼。
    /// </summary>
    private static string GetUserIdentityLabel(UserAccount user)
    {
        // 針對缺乏顯示名稱的使用者，改用 UID 以利追蹤
        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserUid : user.DisplayName!;
    }

    /// <summary>
    /// 清除指定裝置已過期的 Refresh Token，避免資料庫累積垃圾資料。
    /// </summary>
    private async Task CleanupExpiredTokensAsync(string deviceRegistrationUid, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredTokens = await _context.RefreshTokens
            .Where(x => x.DeviceRegistrationUid == deviceRegistrationUid && (x.ExpireAt < now || x.IsRevoked))
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count == 0)
        {
            return;
        }

        _context.RefreshTokens.RemoveRange(expiredTokens);
    }
}
