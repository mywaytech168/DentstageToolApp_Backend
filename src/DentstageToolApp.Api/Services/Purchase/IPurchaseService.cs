using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Purchases;

namespace DentstageToolApp.Api.Services.Purchase;

/// <summary>
/// 採購模組服務介面，提供採購單與類別的維運行為。
/// </summary>
public interface IPurchaseService
{
    /// <summary>
    /// 取得採購單列表。
    /// </summary>
    Task<PurchaseOrderListResponse> GetPurchaseOrdersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 取得單筆採購單。
    /// </summary>
    Task<PurchaseOrderDetailResponse> GetPurchaseOrderAsync(string purchaseOrderUid, CancellationToken cancellationToken);

    /// <summary>
    /// 建立採購單。
    /// </summary>
    Task<PurchaseOrderDetailResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新採購單。
    /// </summary>
    Task<PurchaseOrderDetailResponse> UpdatePurchaseOrderAsync(UpdatePurchaseOrderRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除採購單。
    /// </summary>
    Task DeletePurchaseOrderAsync(string purchaseOrderUid, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 取得採購品項類別列表。
    /// </summary>
    Task<IReadOnlyCollection<PurchaseCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 建立採購品項類別。
    /// </summary>
    Task<PurchaseCategoryDto> CreateCategoryAsync(CreatePurchaseCategoryRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新採購品項類別。
    /// </summary>
    Task<PurchaseCategoryDto> UpdateCategoryAsync(UpdatePurchaseCategoryRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除採購品項類別。
    /// </summary>
    Task DeleteCategoryAsync(string categoryUid, string operatorName, CancellationToken cancellationToken);
}
