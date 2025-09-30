using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 黑名單資料實體，對應資料庫 BlackLists 資料表。
/// </summary>
public class BlackList
{
    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 黑名單識別碼，為主鍵。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 門市識別碼。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 建立使用者識別碼。
    /// </summary>
    public string? UserUid { get; set; }

    /// <summary>
    /// 狀態。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 取消日期。
    /// </summary>
    public DateOnly? CancelDate { get; set; }

    /// <summary>
    /// 預約日期。
    /// </summary>
    public DateOnly? BookDate { get; set; }

    /// <summary>
    /// 修復日期。
    /// </summary>
    public DateOnly? FixDate { get; set; }

    /// <summary>
    /// 顧客識別碼。
    /// </summary>
    public string? CustomerUid { get; set; }

    /// <summary>
    /// 顧客姓名。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 顧客電話。
    /// </summary>
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// 顧客電話過濾欄位。
    /// </summary>
    public string? CustomerPhoneFilter { get; set; }

    /// <summary>
    /// 是否列入黑名單。
    /// </summary>
    public bool? FlagBlack { get; set; }

    /// <summary>
    /// 加入黑名單原因。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 關聯報價單識別碼。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 建立使用者名稱。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 關聯顧客導航屬性。
    /// </summary>
    public Customer? Customer { get; set; }
}
