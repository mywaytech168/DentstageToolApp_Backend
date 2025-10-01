using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 維修類型主檔實體，對應 fix_types 資料表。
/// </summary>
public class FixType
{
    /// <summary>
    /// 維修類型主鍵。
    /// </summary>
    public int FixTypeId { get; set; }

    /// <summary>
    /// 維修類型名稱，例如凹痕、鈑烤等。
    /// </summary>
    public string FixTypeName { get; set; } = null!;

    /// <summary>
    /// 導覽屬性：使用此維修類型的估價單集合。
    /// </summary>
    public ICollection<Quatation> Quatations { get; set; } = new List<Quatation>();
}
