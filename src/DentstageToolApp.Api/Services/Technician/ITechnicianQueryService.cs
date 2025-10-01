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
    /// 依據查詢條件取得技師名單資料。
    /// </summary>
    /// <param name="query">查詢參數，包含店家識別碼。</param>
    /// <param name="cancellationToken">取消權杖，供前端中止操作。</param>
    /// <returns>回傳整理後的技師名單。</returns>
    Task<TechnicianListResponse> GetTechniciansAsync(TechnicianListQuery query, CancellationToken cancellationToken);
}
