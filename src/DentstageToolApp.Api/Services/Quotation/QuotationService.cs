using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Quotations;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DentstageToolApp.Api.Services.Quotation;

/// <summary>
/// 估價單服務實作，透過資料庫查詢回傳估價單列表所需的欄位。
/// </summary>
public class QuotationService : IQuotationService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly DentstageToolAppContext _context;

    /// <summary>
    /// 建構子，注入資料庫內容物件以供查詢使用。
    /// </summary>
    public QuotationService(DentstageToolAppContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<QuotationListResponse> GetQuotationsAsync(QuotationListQuery query, CancellationToken cancellationToken)
    {
        // ---------- 查詢前置處理 ----------
        // 建立安全的分頁設定，避免前端傳入異常數值造成資料庫壓力。
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        // 將結束日期調整為當日 23:59:59，確保包含整日資料。
        DateTime? endDateInclusive = null;
        if (query.EndDate.HasValue)
        {
            endDateInclusive = query.EndDate.Value.Date.AddDays(1);
        }

        // ---------- 建立查詢 ----------
        var quotationsQuery = _context.Quatations
            .AsNoTracking()
            .AsQueryable();

        // 篩選維修類型。
        if (!string.IsNullOrWhiteSpace(query.FixType))
        {
            quotationsQuery = quotationsQuery.Where(q => q.FixType == query.FixType);
        }

        // 篩選估價單狀態。
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            quotationsQuery = quotationsQuery.Where(q => q.Status == query.Status);
        }

        // 篩選建立日期（起始）。
        if (query.StartDate.HasValue)
        {
            var startDate = query.StartDate.Value.Date;
            quotationsQuery = quotationsQuery.Where(q => q.CreationTimestamp >= startDate);
        }

        // 篩選建立日期（結束，包含當日）。
        if (endDateInclusive.HasValue)
        {
            quotationsQuery = quotationsQuery.Where(q => q.CreationTimestamp < endDateInclusive.Value);
        }

        // 客戶關鍵字，模糊搜尋姓名或電話。
        if (!string.IsNullOrWhiteSpace(query.CustomerKeyword))
        {
            var keyword = query.CustomerKeyword.Trim();
            quotationsQuery = quotationsQuery.Where(q =>
                (q.Name != null && EF.Functions.Like(q.Name, $"%{keyword}%")) ||
                (q.Phone != null && EF.Functions.Like(q.Phone, $"%{keyword}%")));
        }

        // 車牌關鍵字，模糊搜尋車牌號碼。
        if (!string.IsNullOrWhiteSpace(query.CarPlateKeyword))
        {
            var plateKeyword = query.CarPlateKeyword.Trim();
            quotationsQuery = quotationsQuery.Where(q =>
                q.CarNo != null && EF.Functions.Like(q.CarNo, $"%{plateKeyword}%"));
        }

        // ---------- 計算總筆數 ----------
        var totalCount = await quotationsQuery.CountAsync(cancellationToken);

        // ---------- 套用排序與分頁 ----------
        var pagedQuery = quotationsQuery
            .OrderByDescending(q => q.CreationTimestamp ?? DateTime.MinValue)
            .ThenByDescending(q => q.QuotationNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuotationSummaryResponse
            {
                QuotationNo = q.QuotationNo,
                Status = q.Status,
                CustomerName = q.Name,
                CustomerPhone = q.Phone,
                CarBrand = q.Brand,
                CarModel = q.Model,
                CarPlateNumber = q.CarNo,
                // 目前僅能取得門市代碼，待補齊門市主檔後改為顯示名稱。
                StoreName = q.StoreUid,
                EstimatorName = q.UserName,
                // 建立人員暫做為製單技師資訊。
                CreatorName = q.CreatedBy,
                CreatedAt = q.CreationTimestamp,
                FixType = q.FixType
            });

        var items = await pagedQuery.ToListAsync(cancellationToken);

        // ---------- 回傳結果 ----------
        return new QuotationListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }
}
