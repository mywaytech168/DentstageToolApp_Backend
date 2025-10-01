using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 技師主檔實體，對應 technicians 資料表。
/// </summary>
public class Technician
{
    /// <summary>
    /// 技師主鍵識別碼。
    /// </summary>
    public int TechnicianId { get; set; }

    /// <summary>
    /// 技師姓名。
    /// </summary>
    public string TechnicianName { get; set; } = null!;

    /// <summary>
    /// 所屬門市識別碼，對應 stores 表的主鍵。
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// 導覽屬性：技師所屬的門市資料。
    /// </summary>
    public Store Store { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：由該技師負責的估價單清單。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();
}
