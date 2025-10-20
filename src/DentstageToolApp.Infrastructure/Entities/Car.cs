using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 車輛主檔實體，對應資料庫 Cars 資料表。
/// </summary>
public class Car
{
    /// <summary>
    /// 建立時間戳記，追蹤車輛資料建立時間。
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
    /// 車輛唯一識別碼，為資料表主鍵。
    /// </summary>
    public string CarUid { get; set; } = null!;

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? CarNo { get; set; }

    /// <summary>
    /// 車牌搜尋欄位。
    /// </summary>
    public string? CarNoQuery { get; set; }

    /// <summary>
    /// 車輛品牌。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? CarRemark { get; set; }

    /// <summary>
    /// 車輛里程數，以公里為單位儲存，允許 null 代表未記錄。
    /// </summary>
    public int? Milage { get; set; }

    /// <summary>
    /// 品牌與型號組合。
    /// </summary>
    public string? BrandModel { get; set; }

    /// <summary>
    /// 車輛關聯的報價單清單。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();

    /// <summary>
    /// 車輛關聯的訂單清單。
    /// </summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
