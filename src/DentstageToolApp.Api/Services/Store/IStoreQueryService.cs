using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Stores;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市查詢服務介面。
/// </summary>
public interface IStoreQueryService
{
    /// <summary>
    /// 取得門市列表。
    /// </summary>
    Task<StoreListResponse> GetStoresAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 透過識別碼取得門市詳細資料。
    /// </summary>
    Task<StoreDetailResponse> GetStoreAsync(string storeUid, CancellationToken cancellationToken);
}
