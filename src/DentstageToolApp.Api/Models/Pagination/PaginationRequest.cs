using System;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Pagination;

/// <summary>
/// 分頁查詢請求物件，統一處理常見的頁碼與每頁筆數設定。
/// </summary>
public class PaginationRequest
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    /// <summary>
    /// 目標頁碼，預設為第一頁。
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "頁碼需大於等於 1。")]
    public int Page { get; set; } = DefaultPage;

    /// <summary>
    /// 每頁筆數，預設 20，並限制最高 200 筆以避免大量撈取。
    /// </summary>
    [Range(1, MaxPageSize, ErrorMessage = "每頁筆數需介於 1 到 200。")]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>
    /// 取得整理後的分頁設定，確保頁碼與每頁筆數皆為有效值。
    /// </summary>
    /// <returns>回傳 (page, pageSize) 的組合。</returns>
    public (int Page, int PageSize) Normalize()
    {
        var normalizedPage = Page < 1 ? DefaultPage : Page;
        var normalizedSize = PageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => PageSize
        };

        return (normalizedPage, normalizedSize);
    }
}
