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
/// 身份驗證服務實作，負責處理帳號驗證、裝置綁定與 Token 發放。
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
        // 以帳號為索引進行查詢，並一併載入裝置註冊與 Token 清單，方便後續檢查狀態
        var user = await _context.UserAccounts
            .Include(x => x.DeviceRegistrations)
            .Include(x => x.RefreshTokens)
            .FirstOrDefaultAsync(x => x.Account == request.Account, cancellationToken);

        if (user is null)
        {
            throw new AuthException(HttpStatusCode.Unauthorized, "帳號或密碼錯誤。");
        }

        if (!user.IsActive)
        {
            throw new AuthException(HttpStatusCode.Forbidden, "帳號已被停用，請聯絡管理員。");
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new AuthException(HttpStatusCode.Unauthorized, "帳號或密碼錯誤。");
        }

        // 檢查或建立裝置註冊資料，確保每台裝置都有綁定紀錄
        var device = user.DeviceRegistrations.FirstOrDefault(x => x.DeviceKey == request.DeviceKey);
        if (device is null)
        {
            device = CreateDeviceRegistration(user, request);
            await _context.DeviceRegistrations.AddAsync(device, cancellationToken);
        }
        else
        {
            UpdateDeviceMetadata(device, request);
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
        device.ExpireAt ??= refreshTokenExpireAt;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("使用者 {Account} 從裝置 {Device} 成功登入。", user.Account, device.DeviceRegistrationUid);

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

        _logger.LogInformation("使用者 {Account} 透過裝置 {Device} 更新 Token。", user.Account, device.DeviceRegistrationUid);

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
    /// 根據登入資訊建立或更新裝置註冊資料。
    /// </summary>
    private static DeviceRegistration CreateDeviceRegistration(UserAccount user, LoginRequest request)
    {
        return new DeviceRegistration
        {
            DeviceRegistrationUid = Guid.NewGuid().ToString("N"),
            UserUid = user.UserUid,
            DeviceKey = request.DeviceKey,
            DeviceName = request.DeviceName,
            Status = "Active",
            IsBlackListed = false,
            CreationTimestamp = DateTime.UtcNow,
            CreatedBy = user.Account,
            UserAccount = user
        };
    }

    /// <summary>
    /// 更新既有裝置的描述資訊與最後修改時間。
    /// </summary>
    private static void UpdateDeviceMetadata(DeviceRegistration device, LoginRequest request)
    {
        device.DeviceName = request.DeviceName ?? device.DeviceName;
        device.ModificationTimestamp = DateTime.UtcNow;
        device.ModifiedBy = request.Account;
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
            new(JwtRegisteredClaimNames.UniqueName, user.Account),
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
            CreatedBy = user.Account,
            UserAccount = user,
            DeviceRegistration = device
        };

        _context.RefreshTokens.Add(refreshToken);
        return refreshToken;
    }

    /// <summary>
    /// 驗證密碼是否符合資料庫儲存的雜湊值。
    /// </summary>
    private static bool VerifyPassword(string plainPassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        // 先假設資料庫儲存 SHA256 雜湊，若比較失敗則回退至明碼比較（為舊資料相容性保留）
        var sha256Hash = ComputeSha256Hash(plainPassword);
        if (string.Equals(sha256Hash, storedHash, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(plainPassword, storedHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// 使用 SHA256 計算雜湊字串。
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
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
