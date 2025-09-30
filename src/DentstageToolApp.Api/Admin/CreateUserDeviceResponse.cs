using System;

namespace DentstageToolApp.Api.Admin;

/// <summary>
/// 管理者建立帳號與裝置後的回應資料，回傳關鍵識別資訊。
/// </summary>
public class CreateUserDeviceResponse
{
    /// <summary>
    /// 新建立的使用者識別碼。
    /// </summary>
    public string UserUid { get; set; } = string.Empty;

    /// <summary>
    /// 使用者顯示名稱。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 使用者角色資訊。
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// 新增裝置註冊的唯一識別碼。
    /// </summary>
    public string DeviceRegistrationUid { get; set; } = string.Empty;

    /// <summary>
    /// 裝置專屬機碼。
    /// </summary>
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// 裝置名稱或註記。
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 裝置目前狀態，預期為 Active。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 裝置過期時間，尚未設定時為 null。
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// 給前端或管理者的提示訊息。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
