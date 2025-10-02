using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 車輛品牌主檔實體，對應資料庫 Brands 資料表。
/// </summary>
public class Brand
{
    /// <summary>
    /// 品牌識別碼，資料表主鍵改為 UID 字串。
    /// </summary>
    public string BrandUid { get; set; } = null!;

    /// <summary>
    /// 品牌名稱。
    /// </summary>
    public string BrandName { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：品牌底下可用的車型清單。
    /// </summary>
    public ICollection<Model> Models { get; set; } = new List<Model>();

    /// <summary>
    /// 導覽屬性：引用此品牌的報價單清單。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();
}
