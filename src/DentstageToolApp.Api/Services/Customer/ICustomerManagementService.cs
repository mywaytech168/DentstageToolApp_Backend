using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Customers;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶維運服務介面，定義新增客戶所需的操作。
/// </summary>
public interface ICustomerManagementService
{
    /// <summary>
    /// 建立新的客戶資料。
    /// </summary>
    /// <param name="request">客戶建立請求內容。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>回傳建立完成的客戶資訊。</returns>
    Task<CreateCustomerResponse> CreateCustomerAsync(CreateCustomerRequest request, string operatorName, CancellationToken cancellationToken);
}
