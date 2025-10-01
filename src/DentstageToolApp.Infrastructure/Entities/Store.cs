using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 門市主檔實體，對應 stores 資料表。
/// </summary>
public class Store
{
    /// <summary>
    /// 門市主鍵識別碼。
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// 門市名稱。
    /// </summary>
    public string StoreName { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：門市底下的技師清單。
    /// </summary>
    public ICollection<Technician> Technicians { get; set; } = new List<Technician>();

    /// <summary>
    /// 導覽屬性：隸屬此門市的估價單集合。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();
}
