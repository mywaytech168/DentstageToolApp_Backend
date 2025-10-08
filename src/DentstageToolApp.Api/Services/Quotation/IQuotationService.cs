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
    /// <param name="operatorContext">操作人員上下文資訊，包含使用者識別碼與顯示名稱。</param>
    /// <param name="cancellationToken">取消權杖，支援前端取消請求。</param>
    Task<CreateQuotationResponse> CreateQuotationAsync(CreateQuotationRequest request, QuotationOperatorContext operatorContext, CancellationToken cancellationToken);

    /// <summary>
    /// 取得單一估價單的詳細資訊。
    /// </summary>
    /// <param name="request">查詢條件，僅需提供估價單編號即可。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task<QuotationDetailResponse> GetQuotationAsync(GetQuotationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 更新估價單的車輛、客戶與類別備註資訊。
    /// </summary>
    /// <param name="request">更新內容。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task UpdateQuotationAsync(UpdateQuotationRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 將估價單標記為估價完成，狀態更新為 180。
    /// </summary>
    /// <param name="request">估價完成請求，需帶入估價單編號。</param>
    /// <param name="operatorName">操作人員名稱。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task<QuotationStatusChangeResponse> CompleteEvaluationAsync(QuotationEvaluateRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 取消估價單或預約，將狀態調整為 195 並記錄操作資訊。
    /// </summary>
    Task<QuotationStatusChangeResponse> CancelQuotationAsync(QuotationCancelRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 將估價單狀態轉為預約，寫入預約日期並回傳異動資訊。
    /// </summary>
    Task<QuotationStatusChangeResponse> ConvertToReservationAsync(QuotationReservationRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 更新已預約估價單的預約日期，維持狀態為 190。
    /// </summary>
    Task<QuotationStatusChangeResponse> UpdateReservationDateAsync(QuotationReservationRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 取消既有預約，保留取消原因並清除預約日期。
    /// </summary>
    Task<QuotationStatusChangeResponse> CancelReservationAsync(QuotationCancelRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 將狀態回朔至上一個有效狀態，供預約取消後誤按回復使用。
    /// </summary>
    Task<QuotationStatusChangeResponse> RevertQuotationStatusAsync(QuotationRevertStatusRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 估價單轉維修，建立維修工單並回傳工單編號。
    /// </summary>
    Task<QuotationMaintenanceConversionResponse> ConvertToMaintenanceAsync(QuotationMaintenanceRequest request, string operatorName, CancellationToken cancellationToken);

    /// <summary>
    /// 產生隨機的估價單建立測試資料，協助前端快速帶入範例內容。
    /// </summary>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>包含測試草稿與摘要資訊的結構。</returns>
    Task<CreateQuotationTestPageResponse> GenerateRandomQuotationTestPageAsync(CancellationToken cancellationToken);
}
