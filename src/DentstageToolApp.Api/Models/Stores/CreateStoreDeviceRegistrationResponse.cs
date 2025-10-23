using System;

namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 門市註冊機碼建立成功後的回應模型，回傳裝置註冊與機碼資訊。
/// </summary>
public class CreateStoreDeviceRegistrationResponse
{
    /// <summary>
    /// 門市識別碼，確認註冊機碼所屬門市。
    /// </summary>
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 對應的使用者帳號識別碼，便於後續查詢門市帳號資料。
    /// </summary>
    public string UserUid { get; set; } = string.Empty;

    /// <summary>
    /// 裝置註冊唯一識別碼，供稽核與管理使用。
    /// </summary>
    public string DeviceRegistrationUid { get; set; } = string.Empty;

    /// <summary>
    /// 系統產生的註冊機碼，App 登入時需使用此值。
    /// </summary>
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// 建立時指定的裝置名稱，可為空代表未填寫。
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 註冊機碼的有效期限，若為 null 代表目前未設定到期日。
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// 註冊機碼建立的時間戳記，採用 UTC 標準時間。
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// 操作結果訊息，提供前端顯示使用。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
