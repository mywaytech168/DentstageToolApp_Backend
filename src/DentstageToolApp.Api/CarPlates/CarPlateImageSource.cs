using System;

namespace DentstageToolApp.Api.CarPlates;

/// <summary>
/// 車牌影像來源，統一封裝檔案名稱與位元組內容。
/// </summary>
public class CarPlateImageSource
{
    /// <summary>
    /// 影像檔案名稱，便於記錄與排錯。
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 圖片的原始位元組內容。
    /// </summary>
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
}
