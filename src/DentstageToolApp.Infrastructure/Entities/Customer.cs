using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 顧客基本資料實體，對應資料庫 Customers 資料表。
/// </summary>
public class Customer
{
    /// <summary>
    /// 建立時間戳記，追蹤資料建立時間。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員帳號或名稱。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記，記錄最後異動時間。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員帳號或名稱。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 顧客唯一識別碼，為資料表主鍵。
    /// </summary>
    public string CustomerUid { get; set; } = null!;

    /// <summary>
    /// 顧客姓名。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 顧客類型。
    /// </summary>
    public string? CustomerType { get; set; }

    /// <summary>
    /// 性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 聯絡方式描述。
    /// </summary>
    public string? Connect { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 聯絡電話索引用欄位。
    /// </summary>
    public string? PhoneQuery { get; set; }

    /// <summary>
    /// 電子郵件。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 年齡區間資訊。
    /// </summary>
    public string? AgeRange { get; set; }

    /// <summary>
    /// 所在縣市。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 所在鄉鎮區。
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
    /// 聯絡備註。
    /// </summary>
    public string? ConnectRemark { get; set; }

    /// <summary>
    /// 聯絡人是否同顧客姓名標記。
    /// </summary>
    public string? ConnectSameAsName { get; set; }

    /// <summary>
    /// 顧客建立的報價單清單。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();

    /// <summary>
    /// 顧客相關的訂單清單。
    /// </summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    /// <summary>
    /// 顧客相關的黑名單紀錄清單。
    /// </summary>
    public ICollection<BlackList> BlackLists { get; set; } = new List<BlackList>();
}
