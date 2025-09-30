using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Quotations;

namespace DentstageToolApp.Api.Services.Quotation;

/// <summary>
/// 估價單服務介面，提供外部呼叫取得估價單列表的能力。
/// </summary>
public interface IQuotationService
{
    /// <summary>
    /// 取得估價單列表資料，後續可依需求加入查詢條件或分頁參數。
    /// </summary>
    /// <param name="cancellationToken">取消權杖，供呼叫端取消長時間查詢。</param>
    /// <returns>估價單摘要資訊集合。</returns>
    Task<IReadOnlyList<QuotationSummaryResponse>> GetQuotationsAsync(CancellationToken cancellationToken);
}
