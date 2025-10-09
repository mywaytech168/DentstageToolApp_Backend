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

    /// <summary>
    /// 編輯既有客戶資料，會檢核電話是否重複並更新聯絡資訊。
    /// </summary>
    /// <param name="request">客戶編輯請求內容。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>回傳編輯完成的客戶資訊。</returns>
    Task<EditCustomerResponse> EditCustomerAsync(EditCustomerRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除既有客戶資料，刪除前會確認是否仍存在關聯報價單或工單。
    /// </summary>
    /// <param name="request">客戶刪除請求內容。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>刪除完成後的確認訊息。</returns>
    Task<DeleteCustomerResponse> DeleteCustomerAsync(DeleteCustomerRequest request, string operatorName, CancellationToken cancellationToken);
}
