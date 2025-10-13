using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Technicians;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Technician;

/// <summary>
/// 技師資料查詢服務實作，負責從資料庫取得特定店家的技師名單。
/// </summary>
public class TechnicianQueryService : ITechnicianQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<TechnicianQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public TechnicianQueryService(DentstageToolAppContext dbContext, ILogger<TechnicianQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TechnicianListResponse> GetTechniciansAsync(string userUid, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUserUid = (userUid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUserUid))
            {
                // 若缺少使用者識別碼，代表權杖已失效或遭竄改，直接拋出可預期例外。
                throw new TechnicianQueryServiceException(HttpStatusCode.BadRequest, "請提供有效的使用者識別碼。");
            }

            // ---------- 門市解析區 ----------
            // 目前帳號與門市以相同 UID 進行綁定，因此可直接使用使用者識別碼回查門市資料。
            var store = await _dbContext.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.StoreUid == normalizedUserUid, cancellationToken);

            if (store is null)
            {
                // 找不到對應門市時回傳 404，提示後台確認使用者與門市的綁定設定。
                throw new TechnicianQueryServiceException(HttpStatusCode.NotFound, "找不到使用者對應的門市資料。");
            }

            // ---------- 查詢組合區 ----------
            // 透過技師主檔資料表過濾店家識別碼，並僅保留必要欄位，降低資料傳輸量。
            var technicians = await _dbContext.Technicians
                .AsNoTracking()
                .Where(technician => technician.StoreUid == store.StoreUid)
                .Select(technician => new TechnicianItem
                {
                    TechnicianUid = technician.TechnicianUid,
                    TechnicianName = technician.TechnicianName
                })
                // EF Core 無法直接翻譯帶有 Comparer 的排序，改用預設排序以確保查詢可被翻譯。 
                .OrderBy(technician => technician.TechnicianName)
                .ToListAsync(cancellationToken);

            // ---------- 組裝回應區 ----------
            // 即便沒有資料也回傳空集合，讓前端可以顯示相對應提示。
            return new TechnicianListResponse
            {
                Items = technicians
            };
        }
        catch (OperationCanceledException)
        {
            // 前端若在等待過程中取消查詢，統一轉換為 499 狀態碼，方便控制器處理。
            _logger.LogInformation("技師名單查詢流程被取消。");
            throw new TechnicianQueryServiceException((HttpStatusCode)499, "查詢已取消，請重新嘗試。");
        }
        catch (TechnicianQueryServiceException)
        {
            // 已知的業務例外直接往外拋出，讓控制器維持相同訊息與狀態碼。
            throw;
        }
        catch (Exception ex)
        {
            // 其他未預期的錯誤記錄詳細資訊並轉換為通用錯誤訊息。
            _logger.LogError(ex, "查詢技師名單時發生未預期錯誤。");
            throw new TechnicianQueryServiceException(HttpStatusCode.InternalServerError, "查詢技師名單發生錯誤，請稍後再試。");
        }
    }
}

/// <summary>
/// 技師查詢專用例外類別，封裝 HTTP 狀態碼便於控制器使用。
/// </summary>
public class TechnicianQueryServiceException : Exception
{
    /// <summary>
    /// 建構子，建立包含狀態碼與訊息的例外物件。
    /// </summary>
    public TechnicianQueryServiceException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 對應的 HTTP 狀態碼，供控制器轉換為 ProblemDetails。
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}
