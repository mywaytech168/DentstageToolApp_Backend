-- 同步相關資料表建置腳本
-- 說明：包含 SyncMachineProfiles、SyncLogs 與 StoreSyncStates 三張表格結構，提供資料庫初始化或補建使用。

-- ---------- SyncMachineProfiles：同步機碼設定 ----------
CREATE TABLE `SyncMachineProfiles` (
    `MachineKey` VARCHAR(150) NOT NULL COMMENT '同步機碼唯一識別，對應應用程式設定',
    `ServerRole` VARCHAR(50) NOT NULL COMMENT '伺服器角色（Central、Direct、Franchise 等）',
    `StoreId` VARCHAR(100) NULL COMMENT '門市代碼，中央端可為 NULL',
    `StoreType` VARCHAR(50) NULL COMMENT '門市型態（直營、加盟等）',
    `IsActive` BIT(1) NOT NULL DEFAULT b'1' COMMENT '是否啟用同步機碼',
    `UpdatedAt` DATETIME NULL COMMENT '最後更新時間',
    `Remark` VARCHAR(255) NULL COMMENT '維運備註',
    PRIMARY KEY (`MachineKey`),
    KEY `IX_SyncMachineProfiles_ServerRole_IsActive` (`ServerRole`, `IsActive`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='同步機碼設定';

-- ---------- SyncLogs：同步異動紀錄 ----------
CREATE TABLE `SyncLogs` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '主鍵流水號',
    `TableName` VARCHAR(100) NOT NULL COMMENT '來源資料表名稱',
    `RecordId` VARCHAR(100) NOT NULL COMMENT '來源資料表主鍵值，複合鍵以逗號分隔',
    `Action` VARCHAR(20) NOT NULL COMMENT '異動類型（Insert、Update、Delete）',
    `UpdatedAt` DATETIME NOT NULL COMMENT '異動時間（UTC）',
    `SourceServer` VARCHAR(50) NULL COMMENT '來源伺服器識別（Central、Store 等）',
    `StoreType` VARCHAR(50) NULL COMMENT '來源門市型態（直營、加盟）',
    `Synced` BIT(1) NOT NULL DEFAULT b'0' COMMENT '是否已同步至目標端',
    `Payload` LONGTEXT NULL COMMENT '異動時的 JSON 快照內容',
    PRIMARY KEY (`Id`),
    KEY `IX_SyncLogs_TableName_StoreType_UpdatedAt` (`TableName`, `StoreType`, `UpdatedAt`),
    KEY `IX_SyncLogs_Synced` (`Synced`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='同步異動紀錄';

-- ---------- StoreSyncStates：門市同步狀態 ----------
CREATE TABLE `StoreSyncStates` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '主鍵流水號',
    `StoreId` VARCHAR(50) NOT NULL COMMENT '門市代碼',
    `StoreType` VARCHAR(50) NOT NULL COMMENT '門市型態',
    `ServerRole` VARCHAR(50) NULL COMMENT '目前連線伺服器角色',
    `ServerIp` VARCHAR(100) NULL COMMENT '目前連線伺服器 IP',
    `LastCursor` VARCHAR(100) NULL COMMENT '下載同步游標',
    `LastUploadTime` DATETIME NULL COMMENT '最近成功上傳時間',
    `LastDownloadTime` DATETIME NULL COMMENT '最近成功下載時間',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_StoreSyncStates_StoreId_StoreType` (`StoreId`, `StoreType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='門市同步狀態紀錄';
