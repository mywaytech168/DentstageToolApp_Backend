using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Cars;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛查詢服務介面，提供列表與明細查詢功能。
/// </summary>
public interface ICarQueryService
{
    /// <summary>
    /// 取得車輛列表，供前端建立下拉或列表使用。
    /// </summary>
    /// <param name="request">分頁條件，指定頁碼與每頁筆數。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>回傳整理後的車輛清單。</returns>
    Task<CarListResponse> GetCarsAsync(PaginationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 透過車輛識別碼取得詳細資料。
    /// </summary>
    /// <param name="carUid">車輛識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>回傳單筆車輛詳細資料。</returns>
    Task<CarDetailResponse> GetCarAsync(string carUid, CancellationToken cancellationToken);
}
