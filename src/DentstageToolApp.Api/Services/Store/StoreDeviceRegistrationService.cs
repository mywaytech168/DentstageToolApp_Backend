using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Stores;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市裝置註冊服務實作，負責檢核門市與帳號資料並產生註冊機碼。
/// </summary>
public class StoreDeviceRegistrationService : IStoreDeviceRegistrationService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<StoreDeviceRegistrationService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public StoreDeviceRegistrationService(DentstageToolAppContext dbContext, ILogger<StoreDeviceRegistrationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateStoreDeviceRegistrationResponse> CreateDeviceRegistrationAsync(
        CreateStoreDeviceRegistrationRequest request,
        string operatorName,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new StoreDeviceRegistrationException(HttpStatusCode.BadRequest, "請提供門市註冊資料。");
        }

        // ---------- 參數整理區 ----------
        var storeUid = NormalizeRequiredText(request.StoreUid, "門市識別碼");
        var deviceName = NormalizeOptionalText(request.DeviceName);
        var operatorLabel = NormalizeOperator(operatorName);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var storeExists = await _dbContext.Stores
            .AsNoTracking()
            .AnyAsync(store => store.StoreUid == storeUid, cancellationToken);

        if (!storeExists)
        {
            throw new StoreDeviceRegistrationException(HttpStatusCode.NotFound, "找不到對應的門市資料，請確認識別碼是否正確。");
        }

        var userAccount = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(user => user.UserUid == storeUid, cancellationToken);

        if (userAccount is null)
        {
            throw new StoreDeviceRegistrationException(HttpStatusCode.Conflict, "門市尚未建立對應的使用者帳號，請先建立帳號後再發放註冊機碼。");
        }

        var now = DateTime.UtcNow;
        var deviceRegistrationUid = Guid.NewGuid().ToString("N");
        var deviceKey = await GenerateUniqueDeviceKeyAsync(cancellationToken);

        // ---------- 實體建立區 ----------
        var entity = new DeviceRegistration
        {
            DeviceRegistrationUid = deviceRegistrationUid,
            UserUid = userAccount.UserUid,
            DeviceKey = deviceKey,
            DeviceName = deviceName,
            Status = "Active",
            IsBlackListed = false,
            CreationTimestamp = now,
            CreatedBy = operatorLabel,
            ModificationTimestamp = now,
            ModifiedBy = operatorLabel,
            ExpireAt = null,
            UserAccount = userAccount
        };

        await _dbContext.DeviceRegistrations.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 為門市 {StoreUid} 建立裝置註冊 {DeviceRegistrationUid}。",
            operatorLabel,
            storeUid,
            deviceRegistrationUid);

        // ---------- 組裝回應區 ----------
        return new CreateStoreDeviceRegistrationResponse
        {
            StoreUid = storeUid,
            UserUid = userAccount.UserUid,
            DeviceRegistrationUid = deviceRegistrationUid,
            DeviceKey = deviceKey,
            DeviceName = deviceName,
            ExpireAt = entity.ExpireAt,
            GeneratedAt = now,
            Message = "已建立門市註冊機碼。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生唯一的裝置機碼，避免重複造成登入衝突。
    /// </summary>
    private async Task<string> GenerateUniqueDeviceKeyAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = Guid.NewGuid().ToString().ToUpperInvariant();
            var exists = await _dbContext.DeviceRegistrations
                .AsNoTracking()
                .AnyAsync(device => device.DeviceKey == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }

        _logger.LogError("多次嘗試產生註冊機碼皆發生碰撞，請檢查機碼產生邏輯。");
        throw new StoreDeviceRegistrationException(HttpStatusCode.InternalServerError, "無法產生唯一的註冊機碼，請稍後再試。");
    }

    /// <summary>
    /// 驗證必填文字欄位，並將內容去除前後空白。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StoreDeviceRegistrationException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 處理可選文字欄位，移除前後空白並將空字串轉為 null。
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// 統一整理操作人員名稱，若無輸入則使用系統預設值。
    /// </summary>
    private static string NormalizeOperator(string operatorName)
    {
        return string.IsNullOrWhiteSpace(operatorName) ? "StoreAPI" : operatorName.Trim();
    }
}
