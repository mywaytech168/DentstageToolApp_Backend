using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Permissions;

/// <summary>
/// 描述權限選單的項目結構，提供前端依階層呈現。
/// </summary>
public class PermissionMenuItemDto
{
    /// <summary>
    /// 選單代碼，方便前端或權限系統辨識功能。
    /// </summary>
    public string MenuCode { get; set; } = string.Empty;

    /// <summary>
    /// 選單顯示名稱。
    /// </summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>
    /// 前端路由位置，協助導向對應頁面。
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// 選單對應圖示代碼，提供 UI 顯示。
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 可見的店家型態清單，使用中文標記直營店或加盟店。
    /// </summary>
    public IReadOnlyCollection<string> AllowedStoreTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 子選單集合，支援多層級權限配置。
    /// </summary>
    public IReadOnlyCollection<PermissionMenuItemDto> Children { get; set; } = Array.Empty<PermissionMenuItemDto>();
}
