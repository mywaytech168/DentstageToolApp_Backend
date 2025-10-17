using System;

namespace DentstageToolApp.Api.Models.Sync;

/// <summary>
/// 手動補掛同步紀錄的請求內容，指定資料表與主鍵後重新產生 SyncLog。
/// </summary>
public class ManualSyncLogRequest
{
    /// <summary>
    /// 目標資料表名稱（與資料庫實際表名一致）。
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// 指定資料表的主鍵值，複合鍵以逗號分隔。
    /// </summary>
    public string RecordId { get; set; } = null!;

    /// <summary>
    /// 同步動作，預設為 UPDATE，可接受 INSERT/UPDATE/UPSERT/DELETE。
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// 門市識別碼，會寫入 SyncLog.SourceServer。
    /// </summary>
    public string StoreId { get; set; } = null!;

    /// <summary>
    /// 門市型態（直營或連盟），會寫入 SyncLog.StoreType。
    /// </summary>
    public string StoreType { get; set; } = null!;
}

/// <summary>
/// 手動補掛同步紀錄的結果，回傳建立出的 SyncLog 資訊。
/// </summary>
public class ManualSyncLogResponse
{
    /// <summary>
    /// 新建立的同步紀錄識別碼。
    /// </summary>
    public Guid LogId { get; set; }

    /// <summary>
    /// 同步紀錄的同步時間（伺服器時間）。
    /// </summary>
    public DateTime SyncedAt { get; set; }
}
