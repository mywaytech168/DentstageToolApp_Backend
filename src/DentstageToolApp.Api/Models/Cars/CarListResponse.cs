using System;
using System.Collections.Generic;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Models.Cars;

/// <summary>
/// 車輛列表回應物件，提供前端顯示車輛清單使用。
/// </summary>
public class CarListResponse
{
    /// <summary>
    /// 車輛摘要集合。
    /// </summary>
    public List<CarListItem> Items { get; set; } = new();

    /// <summary>
    /// 分頁資訊，提供前端掌握目前頁碼與總筆數。
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();
}

/// <summary>
/// 車輛列表單筆資料，呈現車牌與品牌資訊。
/// </summary>
public class CarListItem
{
    /// <summary>
    /// 車輛識別碼，供明細查詢使用。
    /// </summary>
    public string CarUid { get; set; } = string.Empty;

    /// <summary>
    /// 車輛品牌識別碼，方便列表直接顯示對應品牌。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車牌號碼。
    /// </summary>
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// 車輛品牌名稱。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號識別碼，供前端列表標示所屬車型。
    /// </summary>
    public string? ModelUid { get; set; }

    /// <summary>
    /// 車輛型號名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車輛里程數（公里），方便列表直接顯示基本里程資訊。
    /// </summary>
    public int? Mileage { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
