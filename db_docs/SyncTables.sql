-- 同步相關資料表建置與欄位補充說明
-- 說明：StoreSyncStates 與 SyncMachineProfiles 已整併至 UserAccounts 表，
--       門市同步狀態改由 UserAccounts.UserUid（對應 StoreId）、Role（對應 StoreType）、LastUploadTime 等欄位記錄。

-- ---------- SyncLogs：同步異動紀錄 ----------
CREATE TABLE `SyncLogs` (
    `Id` CHAR(36) NOT NULL COMMENT '主鍵識別碼，使用 Guid 儲存跨節點一致性',
    `TableName` VARCHAR(100) NOT NULL COMMENT '來源資料表名稱',
    `RecordId` VARCHAR(100) NOT NULL COMMENT '來源資料表主鍵值，複合鍵以逗號分隔',
    `Action` VARCHAR(20) NOT NULL COMMENT '異動類型（Insert、Update、Delete）',
    `UpdatedAt` DATETIME NOT NULL COMMENT '資料本身更新時間（UTC）',
    `SyncedAt` DATETIME NOT NULL COMMENT '異動寫入伺服器的時間戳',
    `SourceServer` VARCHAR(100) NULL COMMENT '來源伺服器識別，門市採用 StoreId',
    `StoreType` VARCHAR(50) NULL COMMENT '來源門市型態（直營、加盟）',
    `Synced` BIT(1) NOT NULL DEFAULT b'0' COMMENT '是否已同步至目標端',
    `Payload` LONGTEXT NULL COMMENT '異動時的 JSON 快照內容',
    PRIMARY KEY (`Id`),
    KEY `IX_SyncLogs_TableName_StoreType_SyncedAt` (`TableName`, `StoreType`, `SyncedAt`),
    KEY `IX_SyncLogs_Synced` (`Synced`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='同步異動紀錄';

-- ---------- UserAccounts：同步欄位補充 ----------
ALTER TABLE `UserAccounts`
    ADD COLUMN `ServerIp` VARCHAR(100) NULL COMMENT '伺服器對外 IP',
    ADD COLUMN `LastUploadTime` DATETIME NULL COMMENT '最近一次同步上傳完成時間',
    ADD COLUMN `LastDownloadTime` DATETIME NULL COMMENT '最近一次同步下載完成時間',
    ADD COLUMN `LastSyncCount` INT NOT NULL DEFAULT 0 COMMENT '最近一次同步成功處理的筆數';
