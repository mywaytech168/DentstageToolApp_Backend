using System;

namespace DentstageToolApp.Api.Cars;

/// <summary>
/// 新增車輛成功後回傳給前端的結果資訊。
/// </summary>
public class CreateCarResponse
{
    /// <summary>
    /// 車輛主鍵識別碼，供後續編輯或查詢使用。
    /// </summary>
    public string CarUid { get; set; } = null!;

    /// <summary>
    /// 車牌號碼，會維持使用者輸入的大寫格式。
    /// </summary>
    public string LicensePlateNumber { get; set; } = null!;

    /// <summary>
    /// 車輛品牌或車款資訊。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號資訊。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 建立時間，以 UTC 儲存供前端轉換時區。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 人類可讀的提示訊息，方便前端直接顯示。
    /// </summary>
    public string Message { get; set; } = null!;
}
