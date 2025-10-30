using System;

namespace DentstageToolApp.Api.Models.Options;

/// <summary>
/// 維修單相關設定值，集中管理退傭驗證等安全性參數。
/// </summary>
public class MaintenanceOrderOptions
{
    /// <summary>
    /// 退傭密碼（純文字或外部雜湊比對結果），僅供後台操作人員驗證使用。
    /// </summary>
    public string? RebatePassword { get; set; }

    /// <summary>
    /// 退傭密碼的雜湊值，若有提供則會優先採用雜湊驗證邏輯。
    /// </summary>
    public string? RebatePasswordHash { get; set; }

    /// <summary>
    /// 退傭密碼雜湊所使用的鹽值，搭配 <see cref="RebatePasswordHash"/> 進行比對。
    /// </summary>
    public string? RebatePasswordSalt { get; set; }

    /// <summary>
    /// 退傭密碼的雜湊演算法識別文字，預設使用 SHA256。
    /// </summary>
    public string HashAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// 雜湊比對的時間常數（毫秒），避免因為驗證速度不同造成計時側信道。
    /// </summary>
    public int ConstantTimePaddingMilliseconds { get; set; } = 15;
}
