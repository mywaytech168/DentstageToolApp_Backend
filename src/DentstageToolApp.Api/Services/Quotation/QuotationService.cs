using System;
using System.Collections.Generic;
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
    private readonly DentstageToolAppContext _context;

    /// <summary>
    /// 建構子，注入資料庫內容物件以供查詢使用。
    /// </summary>
    public QuotationService(DentstageToolAppContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuotationSummaryResponse>> GetQuotationsAsync(CancellationToken cancellationToken)
    {
        // 後續若需加入條件（例如狀態、門市、日期區間）可於此延伸 Queryable
        var query = _context.Quatations
            .AsNoTracking()
            .OrderByDescending(q => q.CreationTimestamp ?? DateTime.MinValue)
            .Select(q => new QuotationSummaryResponse
            {
                QuotationNo = q.QuotationNo,
                Status = q.Status,
                CustomerName = q.Name,
                CustomerPhone = q.Phone,
                CarBrand = q.Brand,
                CarModel = q.Model,
                CarPlateNumber = q.CarNo,
                // 目前僅能取得門市代碼，待補齊門市主檔後改為顯示名稱
                StoreName = q.StoreUid,
                EstimatorName = q.UserName,
                // 建立人員暫做為製單技師資訊
                CreatorName = q.CreatedBy,
                CreatedAt = q.CreationTimestamp
            });

        var results = await query.ToListAsync(cancellationToken);
        return results;
    }
}
