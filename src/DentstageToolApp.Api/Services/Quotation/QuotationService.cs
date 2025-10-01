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
        // 使用 LEFT JOIN 連結 Brands 與 Models 主檔，優先以主檔名稱回傳品牌與車型資訊。
        // 透過多個 LEFT JOIN 串接主檔，優先取得標準化名稱供前端顯示。
        var orderedQuery =
            from quotation in quotationsQuery
            join brand in _context.Brands.AsNoTracking()
                on quotation.BrandId equals brand.BrandId into brandGroup
            from brand in brandGroup.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking()
                on quotation.ModelId equals model.ModelId into modelGroup
            from model in modelGroup.DefaultIfEmpty()
            join fixType in _context.FixTypes.AsNoTracking()
                on quotation.FixTypeId equals fixType.FixTypeId into fixTypeGroup
            from fixType in fixTypeGroup.DefaultIfEmpty()
            join store in _context.Stores.AsNoTracking()
                on quotation.StoreId equals store.StoreId into storeGroup
            from store in storeGroup.DefaultIfEmpty()
            join technician in _context.Technicians.AsNoTracking()
                on quotation.TechnicianId equals technician.TechnicianId into technicianGroup
            from technician in technicianGroup.DefaultIfEmpty()
            orderby quotation.CreationTimestamp ?? DateTime.MinValue descending,
                quotation.QuotationNo descending
            select new { quotation, brand, model, fixType, store, technician };

        var pagedQuery = orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(result => new QuotationSummaryResponse
            {
                QuotationNo = result.quotation.QuotationNo,
                Status = result.quotation.Status,
                CustomerName = result.quotation.Name,
                CustomerPhone = result.quotation.Phone,
                CarBrand = result.brand != null ? result.brand.BrandName : result.quotation.Brand,
                CarModel = result.model != null ? result.model.ModelName : result.quotation.Model,
                CarPlateNumber = result.quotation.CarNo,
                // 門市名稱優先採用主檔資料，若關聯不存在則回落至原欄位。
                StoreName = result.store != null ? result.store.StoreName : result.quotation.CurrentStatusUser,
                // 估價技師同樣先以主檔名稱為主。
                EstimatorName = result.technician != null ? result.technician.TechnicianName : result.quotation.UserName,
                // 建立人員暫做為製單技師資訊。
                CreatorName = result.quotation.CreatedBy,
                CreatedAt = result.quotation.CreationTimestamp,
                // 維修類型若有主檔，回傳主檔名稱，否則回退舊有欄位。
                FixType = result.fixType != null ? result.fixType.FixTypeName : result.quotation.FixType
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
