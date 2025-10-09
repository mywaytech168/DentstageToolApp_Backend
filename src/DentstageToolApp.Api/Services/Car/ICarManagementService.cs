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
    /// <param name="operatorName">操作人員名稱，將由 JWT 使用者資訊帶入。</param>
    /// <param name="cancellationToken">取消權杖，用於中止長時間操作。</param>
    /// <returns>建立成功後的車輛資訊。</returns>
    Task<CreateCarResponse> CreateCarAsync(CreateCarRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 編輯既有車輛資料，會同步檢核車牌是否重複並更新品牌資訊。
    /// </summary>
    /// <param name="request">車輛編輯請求內容。</param>
    /// <param name="operatorName">操作人員名稱，將由 JWT 使用者資訊帶入。</param>
    /// <param name="cancellationToken">取消權杖，用於中止長時間操作。</param>
    /// <returns>回傳編輯完成的車輛資訊。</returns>
    Task<EditCarResponse> EditCarAsync(EditCarRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除既有車輛資料，刪除前會檢查是否仍被報價單或工單引用。
    /// </summary>
    /// <param name="request">車輛刪除請求內容。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>刪除完成後的確認訊息。</returns>
    Task<DeleteCarResponse> DeleteCarAsync(DeleteCarRequest request, string operatorName, CancellationToken cancellationToken);

}
