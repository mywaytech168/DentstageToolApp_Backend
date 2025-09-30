using System;
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
    /// OpenALPR CLI 執行檔完整路徑，例如 /usr/bin/alpr。
    /// </summary>
    [Required]
    public string ExecutablePath { get; set; } = "/usr/bin/alpr";

    /// <summary>
    /// 自訂 openalpr.conf 組態檔絕對路徑，若留空則使用預設路徑。
    /// </summary>
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// runtime_data 目錄絕對路徑，若留空則使用預設資料夾。
    /// </summary>
    public string? RuntimeDataDirectory { get; set; }

    /// <summary>
    /// 額外命令列參數，依序傳入 OpenALPR CLI，例如限制候選數量或模式。
    /// </summary>
    public string[] AdditionalArguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 允許 OpenALPR CLI 執行的最長秒數，預設 15 秒避免程序掛起。
    /// </summary>
    [Range(1, 120)]
    public int ProcessTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 自訂暫存影像的資料夾，若留空則採用系統 Temp 目錄。
    /// </summary>
    public string? TemporaryImageDirectory { get; set; }
}
