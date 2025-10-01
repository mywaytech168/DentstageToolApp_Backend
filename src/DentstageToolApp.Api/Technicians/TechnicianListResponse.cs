using System.Collections.Generic;

namespace DentstageToolApp.Api.Technicians;

/// <summary>
/// 技師名單回應物件，提供前端建立下拉選單或列表使用。
/// </summary>
public class TechnicianListResponse
{
    /// <summary>
    /// 技師清單集合，依照名稱排序後回傳。
    /// </summary>
    public List<TechnicianItem> Items { get; set; } = new();
}

/// <summary>
/// 技師資訊資料列，包含識別碼與名稱。
/// </summary>
public class TechnicianItem
{
    /// <summary>
    /// 技師識別碼，對應後端技師主鍵。
    /// </summary>
    public int TechnicianId { get; set; }

    /// <summary>
    /// 技師姓名，提供前端顯示。
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;
}
