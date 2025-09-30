using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Options;

/// <summary>
/// OpenALPR 組態設定，集中管理國別、模型與授權相關資訊。
/// </summary>
public class OpenAlprOptions
{
    /// <summary>
    /// 模型訓練使用的國碼，例如臺灣可使用 tw、美國使用 us。
    /// </summary>
    [Required]
    public string Country { get; set; } = "tw";

    /// <summary>
    /// 地區代碼，可指定城市或洲別以提高辨識率。
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// openalpr.conf 組態檔絕對路徑，用於載入模型參數。
    /// </summary>
    [Required]
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// runtime_data 目錄絕對路徑，存放偵測所需的模型資料。
    /// </summary>
    [Required]
    public string? RuntimeDataDirectory { get; set; }

    /// <summary>
    /// 雲端 API 授權金鑰，當使用雲端版本時提供；若為離線模式可忽略。
    /// </summary>
    public string? ApiKey { get; set; }
}
