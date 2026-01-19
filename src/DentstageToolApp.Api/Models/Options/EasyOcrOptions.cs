using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// EasyOCR 組態設定（基於 Python 的 OCR 引擎）。
/// 支援 Local 模式（本機 Python 腳本）和 API 模式（遠端 HTTP API）。
/// </summary>
public class EasyOcrOptions
{
    /// <summary>
    /// 是否使用本機模式執行 EasyOCR。
    /// true：使用本機 Python 腳本；false：呼叫遠端 API。
    /// </summary>
    public bool UseLocal { get; set; } = true;

    /// <summary>
    /// EasyOCR API 網址（API 模式時使用）。
    /// 例如："http://localhost:5000/api/ocr" 或 "https://ocr-service.example.com/recognize"
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Python 執行檔路徑（Local 模式時使用，例如 "python" 或 "C:/Python39/python.exe"）。
    /// </summary>
    public string PythonPath { get; set; } = "python";

    /// <summary>
    /// EasyOCR 辨識腳本路徑（Local 模式時使用）。
    /// </summary>
    public string ScriptPath { get; set; } = "scripts/easyocr_recognition.py";

    /// <summary>
    /// 支援的語言列表（例如 ["en", "ch_tra"]）。
    /// </summary>
    public List<string> Languages { get; set; } = new() { "en" };

    /// <summary>
    /// 是否啟用 GPU 加速（需要 CUDA 支援）。
    /// </summary>
    public bool GpuEnabled { get; set; } = false;
}
