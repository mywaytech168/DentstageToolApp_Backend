using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Admin;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Admin;

/// <summary>
/// 管理者帳號維運服務，負責建立使用者與裝置基礎資料。
/// </summary>
public class AccountAdminService : IAccountAdminService
{
    private readonly DentstageToolAppContext _context;
    private readonly ILogger<AccountAdminService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容與記錄器。
    /// </summary>
    public AccountAdminService(DentstageToolAppContext context, ILogger<AccountAdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateUserDeviceResponse> CreateUserWithDeviceAsync(CreateUserDeviceRequest request, CancellationToken cancellationToken)
    {
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        var deviceKey = (request.DeviceKey ?? string.Empty).Trim();
        var role = string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role.Trim();
        var deviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? null : request.DeviceName.Trim();
        var operatorName = string.IsNullOrWhiteSpace(request.OperatorName) ? "AdminAPI" : request.OperatorName.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            // 透過服務內再次驗證，避免僅輸入空白字元
            throw new AccountAdminException(HttpStatusCode.BadRequest, "顯示名稱不可為空白。");
        }

        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            // 防止空白機碼寫入資料庫造成後續登入異常
            throw new AccountAdminException(HttpStatusCode.BadRequest, "裝置機碼不可為空白。");
        }

        // 檢查裝置機碼是否已被使用，避免重複註冊造成登入衝突
        var deviceExists = await _context.DeviceRegistrations
            .AnyAsync(x => x.DeviceKey == deviceKey, cancellationToken);

        if (deviceExists)
        {
            throw new AccountAdminException(HttpStatusCode.Conflict, "裝置機碼已存在，請改用其他機碼。");
        }

        var now = DateTime.UtcNow;
        var userUid = Guid.NewGuid().ToString("N");
        var deviceRegistrationUid = Guid.NewGuid().ToString("N");

        var user = new UserAccount
        {
            UserUid = userUid,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            CreationTimestamp = now,
            CreatedBy = operatorName,
            ModificationTimestamp = now,
            ModifiedBy = operatorName
        };

        var device = new DeviceRegistration
        {
            DeviceRegistrationUid = deviceRegistrationUid,
            UserUid = userUid,
            DeviceKey = deviceKey,
            DeviceName = deviceName,
            Status = "Active",
            IsBlackListed = false,
            CreationTimestamp = now,
            CreatedBy = operatorName,
            ModificationTimestamp = now,
            ModifiedBy = operatorName,
            ExpireAt = null,
            UserAccount = user
        };

        // 透過導覽屬性綁定裝置與使用者，讓 EF Core 自動維護外鍵
        user.DeviceRegistrations.Add(device);

        await _context.UserAccounts.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("管理者 {Operator} 建立使用者 {UserUid} 與裝置 {DeviceUid}。", operatorName, userUid, deviceRegistrationUid);

        return new CreateUserDeviceResponse
        {
            UserUid = userUid,
            DisplayName = user.DisplayName,
            Role = user.Role,
            DeviceRegistrationUid = deviceRegistrationUid,
            DeviceKey = device.DeviceKey,
            DeviceName = device.DeviceName,
            Status = device.Status,
            ExpireAt = device.ExpireAt,
            Message = "已建立帳號與裝置機碼。"
        };
    }

    /// <inheritdoc />
    public async Task<AdminAccountDetailResponse> GetAccountAsync(string userUid, CancellationToken cancellationToken)
    {
        // 先行修剪輸入，避免僅輸入空白造成查詢錯誤。
        var normalizedUserUid = (userUid ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedUserUid))
        {
            // 若未提供有效識別碼，回傳 400 提醒呼叫端補足參數。
            throw new AccountAdminException(HttpStatusCode.BadRequest, "使用者識別碼不可為空白。");
        }

        // 使用 AsNoTracking 避免查詢造成快取追蹤，提升查詢效能。
        var user = await _context.UserAccounts
            .AsNoTracking()
            .Include(x => x.DeviceRegistrations)
            .FirstOrDefaultAsync(x => x.UserUid == normalizedUserUid, cancellationToken);

        if (user == null)
        {
            // 查無資料時以 404 告知前端，便於顯示友善訊息。
            throw new AccountAdminException(HttpStatusCode.NotFound, "找不到指定的使用者帳號。");
        }

        // 依修改時間排序裝置清單，優先呈現最近異動的設備。
        var devices = user.DeviceRegistrations
            .OrderByDescending(d => d.ModificationTimestamp ?? d.CreationTimestamp ?? DateTime.MinValue)
            .Select(d => new AdminAccountDeviceInfo
            {
                DeviceRegistrationUid = d.DeviceRegistrationUid,
                DeviceKey = d.DeviceKey,
                DeviceName = d.DeviceName,
                Status = d.Status,
                IsBlackListed = d.IsBlackListed,
                ExpireAt = d.ExpireAt,
                LastSignInAt = d.LastSignInAt
            })
            .ToList();

        // 透過角色判斷店家型態，供前端決定顯示內容與權限。
        var storeType = DeriveStoreTypeFromRole(user.Role);

        return new AdminAccountDetailResponse
        {
            UserUid = user.UserUid,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            Store = new AdminAccountStoreInfo
            {
                StoreName = user.DisplayName,
                PermissionRole = user.Role,
                StoreType = storeType
            },
            Devices = devices
        };
    }

    /// <summary>
    /// 根據角色字串推論店家型態，若無法判斷則以原字串回傳。
    /// </summary>
    private static string DeriveStoreTypeFromRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            // 預設視為直營店，確保至少能回傳一個明確的型態。
            return "直營店";
        }

        var normalizedRole = role.Trim();

        if (normalizedRole.Contains("加盟", StringComparison.OrdinalIgnoreCase) || normalizedRole.Contains("franchise", StringComparison.OrdinalIgnoreCase))
        {
            return "加盟店";
        }

        if (normalizedRole.Contains("直營", StringComparison.OrdinalIgnoreCase) || normalizedRole.Contains("direct", StringComparison.OrdinalIgnoreCase))
        {
            return "直營店";
        }

        // 若角色名稱非預期字詞，直接回傳原字串保留資訊。
        return normalizedRole;
    }
}
