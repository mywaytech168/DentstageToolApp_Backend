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
| `store_sync_states` | 紀錄門市最後同步狀態，便於中央下發差異時快速查詢 | `StoreId`, `StoreType`, `ServerRole`, `ServerIp`, `LastUploadTime`, `LastDownloadTime`, `LastCursor` |
| `sync_machine_profiles` | 以同步機碼管理伺服器角色與門市資訊 | `MachineKey`, `ServerRole`, `StoreId`, `StoreType`, `IsActive`, `UpdatedAt` |

- 由應用程式層的 `DentstageToolAppContext` 在 `SaveChanges` / `SaveChangesAsync` 進行追蹤，將所有新增、更新、刪除的實體轉換為 `sync_logs`，完全取代資料庫 Trigger。
- 中央伺服器會在同步成功後更新 `store_sync_states`，並同時寫入 `ServerRole` 與 `ServerIp` 區分中央或門市來源。
- 若中央無法從連線取得 IP，可讓門市在請求內帶入 `serverIp` 欄位供紀錄使用。
- `sync_machine_profiles` 由營運單位預先建立（以「裝置機碼」為索引），程式啟動時只需在設定檔指定 `MachineKey` 即可載入伺服器角色與門市資料。
- 使用者登入與背景排程都會依據 `MachineKey` 查詢 `sync_machine_profiles`，JWT Token 與 `SyncUploadRequest` 會自動帶入 `serverRole`、`storeId`、`storeType`。

## 同步流程
1. **分店上傳**
   - 定期查詢 `sync_logs` 中 `Synced = 0` 的異動資料。
   - 打包成 JSON （包含 `StoreId`、`TableName`、`Action`、`Payload` 等）送至中央 API。
   - 中央伺服器依照 Action 進行 Upsert 或刪除，再寫入中央 `sync_logs` 做稽核，並更新 `store_sync_states` 的伺服器角色、IP 與最後上傳時間。
   - 回傳成功後，分店將該批 `Synced` 設為 1。

2. **中央下發**
   - 分店呼叫 API 並帶入 `StoreId`、`StoreType` 與 `LastSyncTime`（皆可由 `sync_machine_profiles` 推導）。
   - 中央伺服器查詢 `orders.ModificationTimestamp > LastSyncTime` 的資料，同步更新 `store_sync_states` 的伺服器角色、IP 與最後下載時間。
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
| `POST /api/sync/upload` | 分店上傳差異，包含新增、更新、刪除異動清單，需帶入 `storeType`、`serverRole`，並建議附帶 `serverIp`。 |
| `GET /api/sync/changes` | 分店下載差異，依照 `storeId`、`storeType` 與 `lastSyncTime` 回傳最新資料。 |

### 上傳範例
```json
{
  "storeId": "STORE-001",
  "storeType": "Direct",
  "serverRole": "直營",
  "serverIp": "10.1.10.5",
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
  "serverRole": "直營",
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
- `appsettings.json` 新增 `Sync` 區段，通常只需設定 `MachineKey`、`ServerIp` 與排程參數，系統會自動查詢 `sync_machine_profiles` 補齊角色與門市資訊。
- 若因特殊情境無法使用機碼，也可於設定檔直接填入 `ServerRole`、`StoreId`、`StoreType` 作為備援，背景服務會以設定值為優先。
- 若採 RabbitMQ 佇列進行同步，可於 `Sync.Queue` 區段填入主機、佇列與帳密資訊，中央會於啟動時檢核設定完整性。
- 直營／連盟門市會啟動 `StoreSyncBackgroundService`，每次排程都會重新讀取 `sync_machine_profiles`，補齊 `sync_logs` 欄位並產生待上傳的 `SyncUploadRequest`。
- `store_sync_states` 僅由中央伺服器在同步成功後更新，門市端不再直接寫入該表，避免狀態錯亂。
- 中央伺服器角色仍不會啟動該背景工作，僅保留 API 與資料整合功能。

### 應用程式層同步紀錄流程
1. 每當 API 或背景工作透過 EF Core 儲存資料時，`DentstageToolAppContext` 會掃描追蹤中的變更實體。
2. 系統將每筆異動轉為 `SyncLog`（包含資料表、鍵值、動作別與時間）。
3. 若同步流程提供門市編號與型態（例如中央處理上傳時呼叫 `SetSyncLogMetadata` 或門市端依 `sync_machine_profiles` 取得資訊），會自動帶入 `SourceServer`、`StoreType`。
4. 門市背景排程會補齊仍缺少的來源資訊並統計待同步筆數。
5. 異動完成後即有對應 `sync_logs` 可供上傳或稽核，無需倚賴資料庫 Trigger。

```json
"Sync": {
  "MachineKey": "DIRECT-STORE-001",
  "ServerRole": "直營",
  "StoreId": "STORE-001",
  "StoreType": "Direct",
  "ServerIp": "10.1.10.5",
  "Transport": "RabbitMq",
  "BackgroundSyncIntervalMinutes": 60,
  "BackgroundSyncBatchSize": 100,
  "Queue": {
    "HostName": "mq.internal", 
    "VirtualHost": "/dentstage",
    "UserName": "sync-user",
    "Password": "sync-password",
    "RequestQueue": "dentstage.sync.upload",
    "ResponseQueue": "dentstage.sync.download",
    "TimeoutSeconds": 30
  }
}
```

## 訊息佇列通訊建議
- 若門市與中央的 HTTP 連線品質不佳，可切換 `Sync.Transport` 為 `RabbitMq`，採用 RabbitMQ RPC Pattern 將同步請求封裝為訊息。
- `Queue.RequestQueue` 用於門市上傳差異資料，中央處理完畢後再將回應寫入 `Queue.ResponseQueue`，門市端依據 CorrelationId 取回結果。
- 當 `Sync.Transport` 設為 `RabbitMq` 時，系統會檢查佇列設定是否完整，避免啟動後才發現缺少主機或佇列名稱。
- 佇列傳輸仍需遵守 `sync_logs` 與 `store_sync_states` 的欄位規格，中央在寫入資料時會同步更新伺服器角色與 IP。

## 程式結構建議
- `LocalDbService`：負責讀寫本地 MySQL 與 `sync_log`。
- `RemoteSyncService`：呼叫中央 API 進行上傳／下載。
- `SyncOrchestrator`：排程邏輯，控管上傳與下發順序。
- `Program.cs`：註冊上述服務並設定排程（例如 BackgroundJobs）。

此份設計作為後續開發 `同步 API` 與排程服務的基礎，並確保所有程式碼註解、 Commit 與 PR 均採中文撰寫。
