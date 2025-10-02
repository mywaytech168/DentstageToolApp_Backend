using System.Threading;
using System.Threading.Tasks;

namespace DentstageToolApp.Api.Infrastructure.Database;

/// <summary>
/// 資料庫結構初始化服務介面，負責在應用程式啟動時確保必要欄位存在。
/// </summary>
public interface IDatabaseSchemaInitializer
{
    /// <summary>
    /// 確保估價單資料表具備技師 UID 欄位，避免查詢舊資料時發生欄位不存在的錯誤。
    /// </summary>
    /// <param name="cancellationToken">取消權杖，可由主流程控制初始化中止。</param>
    Task EnsureQuotationTechnicianColumnAsync(CancellationToken cancellationToken = default);
}
