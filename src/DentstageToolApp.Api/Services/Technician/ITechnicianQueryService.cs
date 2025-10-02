using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Technicians;

namespace DentstageToolApp.Api.Services.Technician;

/// <summary>
/// 技師資料查詢服務介面，負責提供店家技師名單。
/// </summary>
public interface ITechnicianQueryService
{
    /// <summary>
    /// 依據目前登入者的使用者識別碼取得技師名單資料。
    /// </summary>
    /// <param name="userUid">登入者的唯一識別碼，作為反查門市的依據。</param>
    /// <param name="cancellationToken">取消權杖，供前端中止操作。</param>
    /// <returns>回傳整理後的技師名單。</returns>
    Task<TechnicianListResponse> GetTechniciansAsync(string userUid, CancellationToken cancellationToken);
}
