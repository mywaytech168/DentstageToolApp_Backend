using System;
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
    /// 門市型態（例如直營店、連盟店）。
    /// </summary>
    public string StoreType { get; set; } = null!;

    /// <summary>
    /// 門市伺服器角色（直營、連盟），用來建立 store_sync_states 的來源分類。
    /// </summary>
    public string? ServerRole { get; set; }

    /// <summary>
    /// 門市伺服器對外 IP，若中央無法從連線取得可由門市自行填入。
    /// </summary>
    public string? ServerIp { get; set; }

    /// <summary>
    /// 單筆異動資料。
    /// </summary>
    public SyncChangeDto? Change { get; set; }
}

/// <summary>
/// 單筆同步異動描述。
/// </summary>
public class SyncChangeDto
{
    /// <summary>
    /// 同步紀錄主鍵識別碼，沿用門市端的 SyncLog.Id，便於中央維持一致性。
    /// </summary>
    public Guid? LogId { get; set; }

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
    /// 同步紀錄建立時間（門市寫入 SyncLog 的時間），用於還原 SyncedAt。
    /// </summary>
    public DateTime? SyncedAt { get; set; }

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

/// <summary>
/// 照片同步所需欄位，包含圖片內容與描述資訊。
/// </summary>
public class PhotoSyncPayload
{
    /// <summary>
    /// 照片唯一識別碼。
    /// </summary>
    public string PhotoUid { get; set; } = null!;

    /// <summary>
    /// 對應的估價單識別碼。
    /// </summary>
    public string? QuotationUid { get; set; }

    /// <summary>
    /// 關聯主體識別碼，例如工單或其他資料。
    /// </summary>
    public string? RelatedUid { get; set; }

    /// <summary>
    /// 拍攝位置資訊。
    /// </summary>
    public string? Posion { get; set; }

    /// <summary>
    /// 備註文字。
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 傷痕形狀代碼。
    /// </summary>
    public string? PhotoShape { get; set; }

    /// <summary>
    /// 其他形狀描述。
    /// </summary>
    public string? PhotoShapeOther { get; set; }

    /// <summary>
    /// 形狀顯示文字。
    /// </summary>
    public string? PhotoShapeShow { get; set; }

    /// <summary>
    /// 評估費用。
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// 是否已完成。
    /// </summary>
    public bool? FlagFinish { get; set; }

    /// <summary>
    /// 完成費用。
    /// </summary>
    public decimal? FinishCost { get; set; }

    /// <summary>
    /// 圖片內容的 Base64 字串。
    /// </summary>
    public string? FileContentBase64 { get; set; }

    /// <summary>
    /// 圖片副檔名，包含前導的句點（例如 .jpg）。
    /// </summary>
    public string? FileExtension { get; set; }
}
