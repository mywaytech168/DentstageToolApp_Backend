using System;
using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Customers;

/// <summary>
/// 客戶列表回應物件，提供前端呈現客戶概覽使用。
/// </summary>
public class CustomerListResponse
{
    /// <summary>
    /// 客戶資料列集合，依建立時間排序後回傳。
    /// </summary>
    public List<CustomerListItem> Items { get; set; } = new();

    /// <summary>
    /// 分頁資訊，提供前端計算下一頁與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();
}

/// <summary>
/// 客戶列表單筆資料，提供列表所需的摘要欄位。
/// </summary>
public class CustomerListItem
{
    /// <summary>
    /// 客戶唯一識別碼，供前端進一步查詢細節時使用。
    /// </summary>
    public string CustomerUid { get; set; } = string.Empty;

    /// <summary>
    /// 客戶姓名或公司名稱。
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// 聯絡電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 客戶類別標籤，例如一般客戶、保險公司等。
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 客戶來源資訊，幫助分析投放成效。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 建立時間，前端可依此顯示排序或時間標籤。
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
