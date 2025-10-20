-- ---------- 資料庫欄位更新腳本：技師職稱與預約方式 ----------
-- 說明：本腳本會於既有資料表補齊新的欄位，並採用 IF NOT EXISTS 避免重複新增造成錯誤。

-- ---------- 技師主檔：新增職稱欄位 ----------
ALTER TABLE `technicians`
    ADD COLUMN IF NOT EXISTS `JobTitle` VARCHAR(50) NULL COMMENT '技師職稱，用於顯示技師角色與職責' AFTER `TechnicianName`;

-- ---------- 報價單：補齊預約方式欄位 ----------
ALTER TABLE `Quatations`
    ADD COLUMN IF NOT EXISTS `Book_method` VARCHAR(50) NULL COMMENT '預約方式來源，例如電話預約或 LINE 預約' AFTER `Book_Date`;

-- ---------- 維修工單：補齊預約方式欄位 ----------
ALTER TABLE `Orders`
    ADD COLUMN IF NOT EXISTS `Book_method` VARCHAR(50) NULL COMMENT '預約方式來源，維持估價單與工單資料一致' AFTER `Book_Date`;

