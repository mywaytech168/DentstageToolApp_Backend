using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.BrandModels;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.BrandModels;

/// <summary>
/// 品牌與型號查詢服務實作，負責從資料庫讀取品牌及車型主檔。
/// </summary>
public class BrandModelQueryService : IBrandModelQueryService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<BrandModelQueryService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器供查詢過程使用。
    /// </summary>
    public BrandModelQueryService(DentstageToolAppContext dbContext, ILogger<BrandModelQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BrandModelListResponse> GetBrandModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ---------- 查詢組合區 ----------
            // 預先載入品牌以及底下的型號集合，避免後續多次資料庫往返。
            var brands = await _dbContext.Brands
                .AsNoTracking()
                .Include(brand => brand.Models)
                .ToListAsync(cancellationToken);

            // ---------- 資料整理區 ----------
            // 依照名稱進行排序並轉換為回應模型，維持前端顯示穩定順序。
            var brandModels = brands
                .OrderBy(brand => brand.BrandName, StringComparer.CurrentCulture)
                .Select(brand => new BrandModelItem
                {
                    BrandUid = brand.BrandUid,
                    BrandName = brand.BrandName,
                    Models = brand.Models
                        .OrderBy(model => model.ModelName, StringComparer.CurrentCulture)
                        .Select(model => new BrandModelOption
                        {
                            ModelUid = model.ModelUid,
                            ModelName = model.ModelName
                        })
                        .ToList()
                })
                .ToList();

            // ---------- 組裝回應區 ----------
            // 若查無資料仍回傳空集合，以便前端顯示提示訊息。
            return new BrandModelListResponse
            {
                Items = brandModels
            };
        }
        catch (OperationCanceledException)
        {
            // 將取消視為可預期流程，改以自訂例外統一呈現並維持 499 自訂狀態碼。
            _logger.LogInformation("品牌型號查詢流程被取消。");
            throw new BrandModelQueryServiceException((HttpStatusCode)499, "查詢流程已取消，請重新發送請求。");
        }
        catch (BrandModelQueryServiceException)
        {
            // 若為服務內自行拋出的例外則直接向上拋出，保留錯誤語意。
            throw;
        }
        catch (Exception ex)
        {
            // 其他未預期錯誤統一包裝後再往外拋出，供控制器處理。
            _logger.LogError(ex, "品牌型號查詢發生未預期錯誤。");
            throw new BrandModelQueryServiceException(HttpStatusCode.InternalServerError, "查詢品牌與型號時發生錯誤，請稍後再試。");
        }
    }
}

/// <summary>
/// 品牌型號查詢服務專用例外，封裝狀態碼與訊息便於控制器處理。
/// </summary>
public class BrandModelQueryServiceException : Exception
{
    /// <summary>
    /// 建構子，建立包含 HTTP 狀態碼的自訂例外。
    /// </summary>
    public BrandModelQueryServiceException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 發生錯誤對應的 HTTP 狀態碼，方便控制器統一處理。
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}
