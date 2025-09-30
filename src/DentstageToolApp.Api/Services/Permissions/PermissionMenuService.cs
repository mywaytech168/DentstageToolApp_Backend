using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Permissions;

namespace DentstageToolApp.Api.Services.Permissions;

/// <summary>
/// 權限選單服務，提供依店家型態回傳預設選單的能力。
/// </summary>
public class PermissionMenuService : IPermissionMenuService
{
    // ---------- 常數與欄位 ----------

    /// <summary>
    /// 直營店標籤字串，避免多處硬編碼。
    /// </summary>
    private const string DirectStoreType = "直營店";

    /// <summary>
    /// 加盟店標籤字串，方便統一管理。
    /// </summary>
    private const string FranchiseStoreType = "加盟店";

    /// <summary>
    /// 預設的選單定義集合，以遞迴方式呈現階層。
    /// </summary>
    private readonly IReadOnlyCollection<MenuDefinition> _menuDefinitions;

    /// <summary>
    /// 建構子，初始化選單定義資料。
    /// </summary>
    public PermissionMenuService()
    {
        // 於初始化階段建立靜態選單設定，避免每次呼叫重新配置。
        _menuDefinitions = BuildMenuDefinitions();
    }

    // ---------- API 呼叫區 ----------

    /// <inheritdoc />
    public Task<IReadOnlyCollection<PermissionMenuItemDto>> GetMenuByStoreTypeAsync(string? storeType, string? role, CancellationToken cancellationToken)
    {
        // 正規化輸入字串，確保過濾時使用一致的店型標籤。
        var normalizedStoreType = NormalizeStoreType(storeType);

        // 依店型篩選選單，再轉換為可回傳的 DTO 結構。
        var items = FilterMenusByStoreType(_menuDefinitions, normalizedStoreType);

        // 目前尚未依角色細分權限，但保留參數供未來擴充使用。
        return Task.FromResult<IReadOnlyCollection<PermissionMenuItemDto>>(items);
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 將選單定義轉換為實際回傳資料並依店型過濾。
    /// </summary>
    private static IReadOnlyCollection<PermissionMenuItemDto> FilterMenusByStoreType(IEnumerable<MenuDefinition> definitions, string storeType)
    {
        var results = new List<PermissionMenuItemDto>();

        foreach (var definition in definitions)
        {
            // 若選單限定特定店型且不包含目前店型，直接跳過。
            if (definition.AllowedStoreTypes.Count > 0 && !definition.AllowedStoreTypes.Contains(storeType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // 遞迴處理子選單，確保僅保留可用功能。
            var filteredChildren = FilterMenusByStoreType(definition.Children, storeType);

            // 若本身有子選單但過濾後為空，且當前節點本身不允許顯示則跳過。
            if (definition.Children.Count > 0 && filteredChildren.Count == 0 && definition.AllowedStoreTypes.Count > 0 && !definition.AllowedStoreTypes.Contains(storeType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var dto = new PermissionMenuItemDto
            {
                MenuCode = definition.MenuCode,
                MenuName = definition.MenuName,
                Route = definition.Route,
                Icon = definition.Icon,
                AllowedStoreTypes = definition.AllowedStoreTypes.ToArray(),
                Children = filteredChildren
            };

            results.Add(dto);
        }

        return results;
    }

    /// <summary>
    /// 針對輸入的店型字串進行正規化，兼容中英文輸入。
    /// </summary>
    private static string NormalizeStoreType(string? storeType)
    {
        if (string.IsNullOrWhiteSpace(storeType))
        {
            return DirectStoreType;
        }

        var normalized = storeType.Trim();

        if (normalized.Contains("加盟", StringComparison.OrdinalIgnoreCase) || normalized.Contains("franchise", StringComparison.OrdinalIgnoreCase))
        {
            return FranchiseStoreType;
        }

        if (normalized.Contains("直營", StringComparison.OrdinalIgnoreCase) || normalized.Contains("direct", StringComparison.OrdinalIgnoreCase))
        {
            return DirectStoreType;
        }

        return normalized;
    }

    /// <summary>
    /// 建立靜態選單定義，集中維護所有權限項目。
    /// </summary>
    private static IReadOnlyCollection<MenuDefinition> BuildMenuDefinitions()
    {
        return new List<MenuDefinition>
        {
            new()
            {
                MenuCode = "dashboard",
                MenuName = "首頁儀表板",
                Route = "/dashboard",
                Icon = "Menu",
                AllowedStoreTypes = new[] { DirectStoreType, FranchiseStoreType },
                Children = Array.Empty<MenuDefinition>()
            },
            new()
            {
                MenuCode = "order-management",
                MenuName = "工單管理",
                Route = "/orders",
                Icon = "Tickets",
                AllowedStoreTypes = new[] { DirectStoreType },
                Children = new List<MenuDefinition>
                {
                    new()
                    {
                        MenuCode = "order-list",
                        MenuName = "工單清單",
                        Route = "/orders/list",
                        Icon = "List",
                        AllowedStoreTypes = new[] { DirectStoreType },
                        Children = Array.Empty<MenuDefinition>()
                    },
                    new()
                    {
                        MenuCode = "order-archive",
                        MenuName = "工單封存",
                        Route = "/orders/archive",
                        Icon = "Box",
                        AllowedStoreTypes = new[] { DirectStoreType },
                        Children = Array.Empty<MenuDefinition>()
                    }
                }
            },
            new()
            {
                MenuCode = "franchise-support",
                MenuName = "加盟支援",
                Route = "/franchise",
                Icon = "UserFilled",
                AllowedStoreTypes = new[] { FranchiseStoreType },
                Children = new List<MenuDefinition>
                {
                    new()
                    {
                        MenuCode = "franchise-training",
                        MenuName = "教育訓練",
                        Route = "/franchise/training",
                        Icon = "Reading",
                        AllowedStoreTypes = new[] { FranchiseStoreType },
                        Children = Array.Empty<MenuDefinition>()
                    },
                    new()
                    {
                        MenuCode = "franchise-promotion",
                        MenuName = "行銷素材",
                        Route = "/franchise/promotion",
                        Icon = "Picture",
                        AllowedStoreTypes = new[] { FranchiseStoreType },
                        Children = Array.Empty<MenuDefinition>()
                    }
                }
            },
            new()
            {
                MenuCode = "reports",
                MenuName = "營運報表",
                Route = "/reports",
                Icon = "DataBoard",
                AllowedStoreTypes = new[] { DirectStoreType, FranchiseStoreType },
                Children = new List<MenuDefinition>
                {
                    new()
                    {
                        MenuCode = "reports-performance",
                        MenuName = "績效分析",
                        Route = "/reports/performance",
                        Icon = "TrendCharts",
                        AllowedStoreTypes = new[] { DirectStoreType },
                        Children = Array.Empty<MenuDefinition>()
                    },
                    new()
                    {
                        MenuCode = "reports-summary",
                        MenuName = "月度摘要",
                        Route = "/reports/summary",
                        Icon = "Calendar",
                        AllowedStoreTypes = new[] { DirectStoreType, FranchiseStoreType },
                        Children = Array.Empty<MenuDefinition>()
                    }
                }
            }
        };
    }

    // ---------- 巢狀結構定義 ----------

    /// <summary>
    /// 內部使用的選單定義資料結構，協助維持原始設定。
    /// </summary>
    private sealed class MenuDefinition
    {
        public string MenuCode { get; init; } = string.Empty;

        public string MenuName { get; init; } = string.Empty;

        public string? Route { get; init; }

        public string? Icon { get; init; }

        public IReadOnlyCollection<string> AllowedStoreTypes { get; init; } = Array.Empty<string>();

        public IReadOnlyCollection<MenuDefinition> Children { get; init; } = Array.Empty<MenuDefinition>();
    }
}
