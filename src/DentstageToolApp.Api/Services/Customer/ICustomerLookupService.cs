using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Customers;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶查詢服務介面，提供以電話搜尋客戶與維修統計的方法。
/// </summary>
public interface ICustomerLookupService
{
    /// <summary>
    /// 透過電話號碼搜尋對應客戶資料並統計維修紀錄。
    /// </summary>
    /// <param name="request">電話搜尋的查詢條件。</param>
    /// <param name="cancellationToken">取消權杖，用於中止長時間查詢。</param>
    /// <returns>回傳客戶清單與維修統計資訊。</returns>
    Task<CustomerPhoneSearchResponse> SearchByPhoneAsync(
        CustomerPhoneSearchRequest request,
        CancellationToken cancellationToken);
}
