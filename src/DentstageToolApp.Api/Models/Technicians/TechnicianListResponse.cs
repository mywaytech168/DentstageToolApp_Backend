using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 技師名單回應物件，提供前端建立下拉選單或列表使用。
/// </summary>
public class TechnicianListResponse
{
    /// <summary>
    /// 技師清單集合，依照名稱排序後回傳。
    /// </summary>
    public List<TechnicianItem> Items { get; set; } = new();

    /// <summary>
    /// 回傳技師所屬門市識別碼，便於前端紀錄使用來源。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 回傳技師所屬門市名稱，提升列表資訊完整度。
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// 分頁資訊，協助前端顯示頁碼與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();
}

/// <summary>
/// 技師資訊資料列，包含識別碼與名稱。
/// </summary>
public class TechnicianItem
{
    /// <summary>
    /// 技師識別碼，改為技師 UID。
    /// </summary>
    public string TechnicianUid { get; set; } = string.Empty;

    /// <summary>
    /// 技師姓名，提供前端顯示。
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// 技師職稱，方便前端顯示角色資訊。
    /// </summary>
    public string? JobTitle { get; set; }
}
