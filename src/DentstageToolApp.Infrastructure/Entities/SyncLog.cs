using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 同步紀錄實體，對應資料庫 sync_logs 資料表。
/// </summary>
public class SyncLog
{
    /// <summary>
    /// 主鍵識別碼。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 目標資料表名稱。
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// 資料表內的紀錄識別值。
    /// </summary>
    public string RecordId { get; set; } = null!;

    /// <summary>
    /// 執行動作（新增、更新、刪除）。
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// 異動時間，作為差異比對依據。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 異動來源伺服器，協助辨識是中央或分店。
    /// </summary>
    public string SourceServer { get; set; } = null!;

    /// <summary>
    /// 門市型態（直營店或連盟店），協助區分同步流程。
    /// </summary>
    public string StoreType { get; set; } = null!;

    /// <summary>
    /// 是否已同步至目標端。
    /// </summary>
    public bool Synced { get; set; }
}
