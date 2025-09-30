using System;
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

}
