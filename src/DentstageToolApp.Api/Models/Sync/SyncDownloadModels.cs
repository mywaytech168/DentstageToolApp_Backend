using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Models.Sync;

/// <summary>
/// 下載差異資料的回應模型。
/// </summary>
public class SyncDownloadResponse
{
    /// <summary>
    /// 門市識別碼。
    /// </summary>
    public string StoreId { get; set; } = null!;

    /// <summary>
    /// 門市型態（直營店、連盟店），用於辨識同步流程。
    /// </summary>
    public string StoreType { get; set; } = null!;

    /// <summary>
    /// 本次回傳資料的伺服器時間，用於更新門市的 LastSyncTime。
    /// </summary>
    public DateTime ServerTime { get; set; }

    /// <summary>
    /// 工單差異資料。
    /// </summary>
    public IList<OrderSyncDto> Orders { get; set; } = new List<OrderSyncDto>();
}

/// <summary>
/// 下載差異資料的查詢條件。
/// </summary>
public class SyncDownloadQuery
{
    /// <summary>
    /// 門市識別碼。
    /// </summary>
    public string StoreId { get; set; } = null!;

    /// <summary>
    /// 門市型態（Direct、Franchise 等）。
    /// </summary>
    public string StoreType { get; set; } = null!;

    /// <summary>
    /// 上次同步時間，若為 null 代表全量下載。
    /// </summary>
    public DateTime? LastSyncTime { get; set; }

    /// <summary>
    /// 單次同步的最大筆數，預設 100。
    /// </summary>
    public int PageSize { get; set; } = 100;
}

/// <summary>
/// 上傳結果回傳資訊。
/// </summary>
public class SyncUploadResult
{
    /// <summary>
    /// 成功處理的筆數。
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// 被忽略的筆數（格式錯誤或不支援的資料表）。
    /// </summary>
    public int IgnoredCount { get; set; }
}
