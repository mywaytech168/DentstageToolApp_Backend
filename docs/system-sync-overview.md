# 系統同步設計總覽

## 架構概述
- 以中央伺服器資料庫為最終真實來源，負責整合直營店與連盟店資料。
- 分店透過固定排程上傳差異資料，中央伺服器再回傳最新資料給各分店。
- 核心差異判斷依據 `updated_at`（或對應欄位）以及 `sync_log` 紀錄，避免整批覆蓋。
- 所有同步請求需帶入 `storeId` 與 `storeType`，確保直營與連盟門市的差異流程都能在同一套介面中管理。

```text
                 上傳差異                  下發更新
   ┌───────────────┐                     ┌───────────────┐
   │  直營店資料庫 │                     │  連盟店資料庫 │
   └───────────────┘                     └───────────────┘
             ▲                                  ▲
             │                                  │
             │ 差異 JSON 上傳         差異 JSON 下發
             │                                  │
             ▼                                  ▼
        ┌──────────────────────────────┐
        │          中央伺服器           │
        └──────────────────────────────┘
```

## 資料表規劃
| 表格 | 目的 | 關鍵欄位 |
| ---- | ---- | -------- |
| `orders` | 儲存工單主檔，並提供 `ModificationTimestamp` 做差異判斷 | `OrderUid`, `StoreUid`, `Status`, `Amount`, `ModificationTimestamp` |
| `sync_logs` | 紀錄每筆異動（新增／更新／刪除），供分店上傳與中央追蹤 | `TableName`, `RecordId`, `Action`, `UpdatedAt`, `SourceServer`, `StoreType`, `Synced` |
| `store_sync_states` | 紀錄門市最後同步狀態，便於中央下發差異時快速查詢 | `StoreId`, `StoreType`, `LastUploadTime`, `LastDownloadTime`, `LastCursor` |

- 透過 `db_docs/sync_logs_triggers.sql` 建立 Trigger，於所有業務資料表的新增／修改／刪除時自動插入 `sync_logs`，確保差異來源一致。
- `store_sync_states` 由中央伺服器在同步成功後更新。

## 同步流程
1. **分店上傳**
   - 定期查詢 `sync_logs` 中 `Synced = 0` 的異動資料。
   - 打包成 JSON （包含 `StoreId`、`TableName`、`Action`、`Payload` 等）送至中央 API。
   - 中央伺服器依照 Action 進行 Upsert 或刪除，再寫入中央 `sync_logs` 做稽核。
   - 回傳成功後，分店將該批 `Synced` 設為 1。

2. **中央下發**
   - 分店呼叫 API 並帶入 `LastSyncTime`。
   - 中央伺服器查詢 `orders.ModificationTimestamp > LastSyncTime` 的資料。
   - 回傳差異資料與新的 `LastSyncTime`，分店再以 Upsert 更新本地資料。
   - 若需要分批，可透過 `PageSize`/`LastCursor` 控制。

3. **衝突處理**
   - 採「中央優先」策略：若中央資料較新，直接覆寫分店。
   - 若需更進階的版本管理，可延伸 `Version` 欄位或精確比對 `ModificationTimestamp`。

### 直營與連盟同步整合重點
- 以 `storeType` 區分直營（Direct）與連盟（Franchise）門市，全部走相同 API 與資料流程。
- 中央資料庫會針對每一組 `storeId + storeType` 建立同步狀態，避免不同型態的門市互相覆寫游標。
- `sync_logs` 會記錄 `storeType`，方便排查是由哪一類型門市上傳或需要下發。

## API 設計
| Method | Path | 說明 |
| ------ | ---- | ---- |
| `POST /api/sync/upload` | 分店上傳差異，包含新增、更新、刪除異動清單，需帶入 `storeType`。 |
| `GET /api/sync/changes` | 分店下載差異，依照 `storeId`、`storeType` 與 `lastSyncTime` 回傳最新資料。 |

### 上傳範例
```json
{
  "storeId": "STORE-001",
  "storeType": "Direct",
  "changes": [
    {
      "tableName": "orders",
      "action": "UPDATE",
      "updatedAt": "2024-04-15T10:20:00Z",
      "recordId": "ORD-1001",
      "payload": {
        "orderUid": "ORD-1001",
        "storeUid": "STORE-001",
        "orderNo": "A12345",
        "amount": 1500.00,
        "status": "Completed",
        "modificationTimestamp": "2024-04-15T10:20:00Z"
      }
    }
  ]
}
```

### 下載回應範例
```json
{
  "storeId": "STORE-001",
  "storeType": "Direct",
  "lastSyncTime": "2024-04-15T10:30:00Z",
  "orders": [
    {
      "orderUid": "ORD-1001",
      "storeUid": "STORE-001",
      "orderNo": "A12345",
      "amount": 1500.00,
      "status": "Completed",
      "modificationTimestamp": "2024-04-15T10:20:00Z"
    }
  ]
}
```

## 排程建議
- **分店上傳**：每 5 分鐘執行一次，避免資料堆積。
- **中央下發**：每 5–10 分鐘同步一次，視資料量調整。
- **Sync Log 清理**：每日或每週清除已同步且超過保留期的紀錄。

## 組態與背景排程實作
- `appsettings.json` 新增 `Sync` 區段，用於辨識目前執行個體的角色（中央、直營、連盟）與背景同步頻率。
- 直營／連盟門市需設定 `StoreId`、`StoreType`，並透過 `BackgroundSyncIntervalMinutes` 控制排程週期（預設 60 分鐘）。
- 直營／連盟門市會啟動 `StoreSyncBackgroundService`，定期補齊 `sync_logs` 的 `SourceServer`、`StoreType` 欄位並統計待同步筆數，為後續上傳中央 API 做準備。
- `store_sync_states` 僅由中央伺服器在同步成功後更新，門市端不再直接寫入該表，避免狀態錯亂。
- 中央伺服器角色則不會啟動該背景工作，僅保留 API 與資料整合功能。

```json
"Sync": {
  "ServerRole": "DirectStore",
  "StoreId": "STORE-001",
  "StoreType": "Direct",
  "BackgroundSyncIntervalMinutes": 60,
  "BackgroundSyncBatchSize": 100
}
```

## 程式結構建議
- `LocalDbService`：負責讀寫本地 MySQL 與 `sync_log`。
- `RemoteSyncService`：呼叫中央 API 進行上傳／下載。
- `SyncOrchestrator`：排程邏輯，控管上傳與下發順序。
- `Program.cs`：註冊上述服務並設定排程（例如 BackgroundJobs）。

此份設計作為後續開發 `同步 API` 與排程服務的基礎，並確保所有程式碼註解、 Commit 與 PR 均採中文撰寫。
