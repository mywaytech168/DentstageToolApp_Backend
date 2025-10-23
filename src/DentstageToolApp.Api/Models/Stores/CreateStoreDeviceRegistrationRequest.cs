using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Stores;

/// <summary>
/// 門市建立註冊機碼的請求模型，包含門市識別碼與可選的裝置名稱。
/// </summary>
public class CreateStoreDeviceRegistrationRequest
{
    /// <summary>
    /// 門市識別碼，對應後台門市主檔的 StoreUid。
    /// </summary>
    [Required(ErrorMessage = "請提供門市識別碼。")]
    [MaxLength(100, ErrorMessage = "門市識別碼長度不可超過 100 字元。")]
    public string StoreUid { get; set; } = string.Empty;

    /// <summary>
    /// 裝置名稱，協助後台辨識註冊來源裝置。
    /// </summary>
    [MaxLength(100, ErrorMessage = "裝置名稱長度不可超過 100 字元。")]
    public string? DeviceName { get; set; }
}
