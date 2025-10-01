using DentstageToolApp.Api.Quotations;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>
    /// 建立估價單並回傳基本資訊。
    /// </summary>
    /// <param name="request">建立所需的欄位資料。</param>
    /// <param name="operatorName">操作人員名稱，會填入建立與修改者欄位。</param>
    /// <param name="cancellationToken">取消權杖，支援前端取消請求。</param>
    Task<CreateQuotationResponse> CreateQuotationAsync(CreateQuotationRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 取得單一估價單的詳細資訊。
    /// </summary>
    /// <param name="request">查詢條件，支援以 UID 或編號取得。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task<QuotationDetailResponse> GetQuotationAsync(GetQuotationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 更新估價單的車輛、客戶與類別備註資訊。
    /// </summary>
    /// <param name="request">更新內容。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task UpdateQuotationAsync(UpdateQuotationRequest request, string operatorName, CancellationToken cancellationToken);
}
