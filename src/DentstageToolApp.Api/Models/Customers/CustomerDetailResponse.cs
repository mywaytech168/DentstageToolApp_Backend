using System;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 客戶詳細資料回應物件，提供前端顯示完整欄位使用。
/// </summary>
public class CustomerDetailResponse
{
    /// <summary>
    /// 客戶唯一識別碼。
    /// </summary>
    public string CustomerUid { get; set; } = string.Empty;

    /// <summary>
    /// 客戶姓名或公司名稱。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 客戶類別標籤。
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 性別資訊。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 聯絡方式或稱謂描述。
    /// </summary>
    public string? Connect { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 電子郵件。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 所在縣市。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 所在鄉鎮市區。
    /// </summary>
    public string? Township { get; set; }

    /// <summary>
    /// 來源渠道。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 聯絡原因。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 其他備註資訊。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 最後修改時間戳記。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 建立人員。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 最後修改人員。
    /// </summary>
    public string? ModifiedBy { get; set; }
}
