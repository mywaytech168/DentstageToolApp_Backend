using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Permissions;

namespace DentstageToolApp.Api.Services.Permissions;

/// <summary>
/// 定義權限選單派送服務，依店家型態回傳對應選單。
/// </summary>
public interface IPermissionMenuService
{
    /// <summary>
    /// 依店家型態篩選可用選單，若傳入空值預設為直營店。
    /// </summary>
    Task<IReadOnlyCollection<PermissionMenuItemDto>> GetMenuByStoreTypeAsync(string? storeType, string? role, CancellationToken cancellationToken);
}
