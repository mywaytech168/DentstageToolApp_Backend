using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Cars;

namespace DentstageToolApp.Api.Services.Car;

/// <summary>
/// 車輛維運服務介面，定義新增車輛所需的方法。
/// </summary>
public interface ICarManagementService
{
    /// <summary>
    /// 新增車輛基本資料，會檢查車牌是否重複並自動正規化格式。
    /// </summary>
    /// <param name="request">車輛建立請求內容。</param>
    /// <param name="cancellationToken">取消權杖，用於中止長時間操作。</param>
    /// <returns>建立成功後的車輛資訊。</returns>
    Task<CreateCarResponse> CreateCarAsync(CreateCarRequest request, CancellationToken cancellationToken);
}
