-- 拆裝費功能新增欄位紀錄
-- PhotoData 表新增 DismantlingFee（拆裝費），以 DECIMAL(10,2) 儲存費用金額。
-- 該欄位用於記錄單位損傷部位的拆裝費用，在計算實收金額時與 Cost（估價金額）相加。
-- 計算公式：實收金額 = (Cost + DismantlingFee) × (MaintenanceProgress / 100)
-- 執行前請確認資料庫已備份，並於非生產環境先行驗證。

ALTER TABLE PhotoData
    ADD COLUMN DismantlingFee DECIMAL(10,2) NULL DEFAULT 0 COMMENT '拆裝費，單位為貨幣';
