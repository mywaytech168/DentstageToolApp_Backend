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
    /// 依據查詢條件取得估價單列表資料。
    /// </summary>
    /// <param name="query">查詢參數，包含維修類型、狀態、關鍵字與分頁設定。</param>
    /// <param name="cancellationToken">取消權杖，供呼叫端取消長時間查詢。</param>
    /// <returns>估價單列表與分頁資訊。</returns>
    Task<QuotationListResponse> GetQuotationsAsync(QuotationListQuery query, CancellationToken cancellationToken);
}
