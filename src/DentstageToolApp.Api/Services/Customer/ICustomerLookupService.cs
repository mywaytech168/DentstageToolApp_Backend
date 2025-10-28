using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Customers;
using DentstageToolApp.Api.Models.Pagination;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶查詢服務介面，提供以電話搜尋客戶與維修統計的方法。
/// </summary>
public interface ICustomerLookupService
{
    /// <summary>
    /// 取得客戶列表，依建立時間倒序排列以便前端展示。
    /// </summary>
    /// <param name="request">分頁條件，指定頁碼與每頁筆數。</param>
    /// <param name="cancellationToken">取消權杖，供前端在需要時中止請求。</param>
    /// <returns>回傳整理後的客戶清單。</returns>
    Task<CustomerListResponse> GetCustomersAsync(PaginationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 透過客戶識別碼取得完整客戶資料，供詳細頁使用。
    /// </summary>
    /// <param name="customerUid">客戶唯一識別碼。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>回傳對應客戶的詳細資料。</returns>
    Task<CustomerDetailResponse> GetCustomerAsync(string customerUid, CancellationToken cancellationToken);

    /// <summary>
    /// 透過電話號碼搜尋對應客戶資料並統計維修紀錄。
    /// </summary>
    /// <param name="request">電話搜尋的查詢條件。</param>
    /// <param name="cancellationToken">取消權杖，用於中止長時間查詢。</param>
    /// <returns>回傳客戶清單與維修統計資訊。</returns>
    Task<CustomerPhoneSearchResponse> SearchByPhoneAsync(
        CustomerPhoneSearchRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 透過電話號碼搜尋客戶並同時回傳估價單與維修單清單。
    /// </summary>
    /// <param name="request">電話搜尋的查詢條件。</param>
    /// <param name="cancellationToken">取消權杖，用於中止長時間查詢。</param>
    /// <returns>回傳單一客戶物件與相關估價、維修歷史。</returns>
    Task<CustomerPhoneSearchDetailResponse> SearchCustomerWithDetailsAsync(
        CustomerPhoneSearchRequest request,
        CancellationToken cancellationToken);
}
