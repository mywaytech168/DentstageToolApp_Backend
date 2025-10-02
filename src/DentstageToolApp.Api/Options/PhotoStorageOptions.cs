using System;

namespace DentstageToolApp.Api.Options;

/// <summary>
/// 照片儲存設定，控制檔案實際保存路徑與後續擴充選項。
/// </summary>
public class PhotoStorageOptions
{
    /// <summary>
    /// 照片儲存目錄，若未設定則預設建立在應用程式資料夾內的 App_Data/photos。
    /// </summary>
    public string? RootPath { get; set; }
}
