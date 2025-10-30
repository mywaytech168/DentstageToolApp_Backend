-- 維修單退傭與維修進度需求新增欄位紀錄
-- 1. PhotoData 表新增 MaintenanceProgress（維修進度百分比），以 Decimal(5,2) 儲存 0~100 區間。
-- 2. PhotoData 表將 PhotoStage 調整為 AfterPhotoUID，改以完工照片 UID 建立對應關係。
-- 執行前請確認資料庫已備份，並於非生產環境先行驗證。

ALTER TABLE PhotoData
    ADD COLUMN MaintenanceProgress DECIMAL(5,2) NULL COMMENT '維修進度百分比（0~100）';

-- 若既有環境已經建立 PhotoStage 欄位，可透過下列指令改名並調整長度：
ALTER TABLE PhotoData
    CHANGE COLUMN PhotoStage AfterPhotoUID VARCHAR(100) NULL COMMENT '維修後照片 UID，對應完工影像的 PhotoUID';

-- 全新資料庫或缺少 PhotoStage 欄位時，請改用下列指令直接新增：
-- ALTER TABLE PhotoData
--     ADD COLUMN AfterPhotoUID VARCHAR(100) NULL COMMENT '維修後照片 UID，對應完工影像的 PhotoUID';
