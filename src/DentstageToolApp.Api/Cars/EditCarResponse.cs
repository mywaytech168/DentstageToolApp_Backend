using System;

namespace DentstageToolApp.Api.Cars;

/// <summary>
/// 編輯車輛成功後回傳給前端的結果資訊。
/// </summary>
public class EditCarResponse
{
    /// <summary>
    /// 車輛主鍵識別碼，供前端確認更新對象。
    /// </summary>
    public string CarUid { get; set; } = null!;

    /// <summary>
    /// 車牌號碼，會維持使用者輸入的大寫格式。
    /// </summary>
    public string CarPlateNumber { get; set; } = null!;

    /// <summary>
    /// 車輛品牌識別碼，若未指定品牌則為 null。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車輛品牌名稱。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車輛型號識別碼，若未指定型號則為 null。
    /// </summary>
    public string? ModelUid { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 更新時間，以 UTC 紀錄供前端轉換時區使用。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 人類可讀的提示訊息，方便前端直接顯示。
    /// </summary>
    public string Message { get; set; } = null!;
}
