using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DentstageToolApp.Api.Models.Sync;

/// <summary>
/// 分店上傳同步資料的請求模型。
/// </summary>
public class SyncUploadRequest
{
    /// <summary>
    /// 門市識別碼。
    /// </summary>
    public string StoreId { get; set; } = null!;

    /// <summary>
    /// 門市型態（直營店 Direct、連盟店 Franchise 等）。
    /// </summary>
    public string StoreType { get; set; } = null!;

    /// <summary>
    /// 門市伺服器角色（DirectStore、AllianceStore），用來建立 store_sync_states 的來源分類。
    /// </summary>
    public string? ServerRole { get; set; }

    /// <summary>
    /// 門市伺服器對外 IP，若中央無法從連線取得可由門市自行填入。
    /// </summary>
    public string? ServerIp { get; set; }

    /// <summary>
    /// 異動清單。
    /// </summary>
    public IList<SyncChangeDto> Changes { get; set; } = new List<SyncChangeDto>();
}

/// <summary>
/// 單筆同步異動描述。
/// </summary>
public class SyncChangeDto
{
    /// <summary>
    /// 目標資料表名稱。
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// 異動動作（INSERT、UPDATE、DELETE）。
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// 異動時間。
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 資料表紀錄識別值。
    /// </summary>
    public string RecordId { get; set; } = null!;

    /// <summary>
    /// 真實資料內容，以 JSON 儲存供伺服器解析。
    /// </summary>
    public JsonElement? Payload { get; set; }
}

/// <summary>
/// 工單同步資料的標準欄位。
/// </summary>
public class OrderSyncDto
{
    /// <summary>
    /// 工單唯一識別碼。
    /// </summary>
    public string OrderUid { get; set; } = null!;

    /// <summary>
    /// 工單編號。
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    /// 對應門市識別碼。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 工單金額。
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// 工單狀態。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 修改時間。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 關聯報價單識別碼。
    /// </summary>
    public string? QuatationUid { get; set; }

    /// <summary>
    /// 建立人員。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改人員。
    /// </summary>
    public string? ModifiedBy { get; set; }
}
