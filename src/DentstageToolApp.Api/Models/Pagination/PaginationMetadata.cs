using System;

namespace DentstageToolApp.Api.Models.Pagination;

/// <summary>
/// 分頁資訊回應物件，提供目前頁碼與總筆數等資訊。
/// </summary>
public class PaginationMetadata
{
    /// <summary>
    /// 目前頁碼，從 1 起算。
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每頁筆數，記錄後端實際採用的分頁設定。
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 資料總筆數，協助前端計算是否還有下一頁。
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 總頁數，依據總筆數與每頁筆數自動計算。
    /// </summary>
    public int TotalPages
    {
        get
        {
            if (PageSize <= 0)
            {
                return 0;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }
}
