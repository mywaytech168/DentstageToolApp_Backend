using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Cars;

/// <summary>
/// 車輛列表回應物件，提供前端顯示車輛清單使用。
/// </summary>
public class CarListResponse
{
    /// <summary>
    /// 車輛摘要集合。
    /// </summary>
    public List<CarListItem> Items { get; set; } = new();
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
    /// 車牌號碼。
    /// </summary>
    public string? CarPlateNumber { get; set; }

    /// <summary>
    /// 車輛品牌名稱。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號名稱。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
