using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 技師主檔實體，對應 technicians 資料表。
/// </summary>
public class Technician
{
    /// <summary>
    /// 技師主鍵識別碼，改以 UID 字串表示，對應資料表欄位 TechnicianUID。
    /// </summary>
    public string TechnicianUid { get; set; } = null!;

    /// <summary>
    /// 技師姓名。
    /// </summary>
    public string TechnicianName { get; set; } = null!;

    /// <summary>
    /// 所屬門市識別碼，改為以 UID 字串串接門市主檔。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 導覽屬性：技師所屬的門市資料。
    /// </summary>
    public Store? Store { get; set; }

    /// <summary>
    /// 導覽屬性：由該技師負責的估價單清單。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();
}
