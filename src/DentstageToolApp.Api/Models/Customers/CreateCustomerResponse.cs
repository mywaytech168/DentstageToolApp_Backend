using System;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 新增客戶成功後回傳給前端的結果資訊。
/// </summary>
public class CreateCustomerResponse
{
    /// <summary>
    /// 客戶主鍵識別碼，供後續查詢或編輯使用。
    /// </summary>
    public string CustomerUid { get; set; } = null!;

    /// <summary>
    /// 客戶名稱。
    /// </summary>
    public string CustomerName { get; set; } = null!;

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 客戶類別。
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 客戶性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 所在縣市。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 所在區域或鄉鎮。
    /// </summary>
    public string? Township { get; set; }

    /// <summary>
    /// 電子郵件。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 消息來源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 為何選擇本服務的原因或需求描述。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 其他備註資訊。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 建立時間，以 UTC 紀錄。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 操作完成提示訊息，方便前端直接顯示。
    /// </summary>
    public string Message { get; set; } = null!;
}
