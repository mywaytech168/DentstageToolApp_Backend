using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 車輛型號主檔實體，對應資料庫 Models 資料表。
/// </summary>
public class Model
{
    /// <summary>
    /// 車型識別碼，資料表主鍵改為 UID 字串。
    /// </summary>
    public string ModelUid { get; set; } = null!;

    /// <summary>
    /// 對應品牌識別碼，改為以品牌 UID 串接品牌主檔。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車型名稱。
    /// </summary>
    public string ModelName { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：車型所屬的品牌資料。
    /// </summary>
    public Brand Brand { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：引用此車型的報價單清單。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();
}
