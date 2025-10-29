using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Purchases;

/// <summary>
/// 採購單列表回應物件。
/// </summary>
public class PurchaseOrderListResponse
{
    /// <summary>
    /// 採購單列表資料。
    /// </summary>
    public IReadOnlyCollection<PurchaseOrderDto> Orders { get; set; } = new List<PurchaseOrderDto>();
}
