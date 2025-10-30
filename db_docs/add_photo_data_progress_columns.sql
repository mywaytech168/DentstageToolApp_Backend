-- 維修單退傭與維修進度需求新增欄位紀錄
-- 1. PhotoData 表新增 MaintenanceProgress（維修進度百分比），以 Decimal(5,2) 儲存 0~100 區間。
-- 2. PhotoData 表新增 PhotoStage（照片階段），用於區分維修前後照片，例如 before、after。
-- 執行前請確認資料庫已備份，並於非生產環境先行驗證。

ALTER TABLE PhotoData
    ADD COLUMN MaintenanceProgress DECIMAL(5,2) NULL COMMENT '維修進度百分比（0~100）';

ALTER TABLE PhotoData
    ADD COLUMN PhotoStage VARCHAR(20) NULL COMMENT '照片階段（before/after）';
