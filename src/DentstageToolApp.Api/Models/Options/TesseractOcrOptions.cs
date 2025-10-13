using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// Tesseract OCR 組態設定，統一管理模型語系與字元限制等參數。
/// </summary>
public class TesseractOcrOptions
{
    /// <summary>
    /// tessdata 資料夾的完整路徑，需確保包含指定語系的訓練資料。
    /// </summary>
    [Required]
    public string TessDataPath { get; set; } = string.Empty;

    /// <summary>
    /// 辨識語系代碼，預設使用英文（eng），如需支援多語系可用 "+" 連結。
    /// </summary>
    [Required]
    public string Language { get; set; } = "eng";

    /// <summary>
    /// 可選的字元白名單，限制辨識輸出僅包含特定字元。
    /// </summary>
    public string? CharacterWhitelist { get; set; }

    /// <summary>
    /// 指定 PageSegMode 名稱，控制 Tesseract 的版面分析方式。
    /// </summary>
    public string? PageSegmentationMode { get; set; }
}
