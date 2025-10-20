-- ---------- 資料庫欄位更新腳本：客戶電子郵件與車輛里程數 ----------
-- 說明：
-- 1. 估價與維修單皆需帶出客戶 Email，因此補齊報價單與工單的 Email 欄位。
-- 2. 車輛資料需紀錄里程數，並同步於估價單與工單上呈現里程資訊。
-- 3. 使用 IF NOT EXISTS 避免重複執行腳本時產生錯誤，確保部署流程安全。

-- ---------- 報價單：新增客戶 Email 與車輛里程數欄位 ----------
ALTER TABLE `Quatations`
    ADD COLUMN IF NOT EXISTS `Email` VARCHAR(100) NULL COMMENT '客戶電子郵件，補齊估價單聯絡方式' AFTER `Source`;

ALTER TABLE `Quatations`
    ADD COLUMN IF NOT EXISTS `Milage` INT NULL COMMENT '車輛里程數，記錄估價當下資料' AFTER `Car_Remark`;

-- ---------- 維修工單：新增客戶 Email 與車輛里程數欄位 ----------
ALTER TABLE `Orders`
    ADD COLUMN IF NOT EXISTS `Email` VARCHAR(100) NULL COMMENT '客戶電子郵件，方便維修工單聯絡資訊完整' AFTER `Source`;

ALTER TABLE `Orders`
    ADD COLUMN IF NOT EXISTS `Milage` INT NULL COMMENT '車輛里程數，維修時參考里程資料' AFTER `CarReserved`;

-- ---------- 車輛主檔：新增里程數欄位 ----------
ALTER TABLE `Cars`
    ADD COLUMN IF NOT EXISTS `Milage` INT NULL COMMENT '車輛里程數，以公里為單位記錄' AFTER `Car_Remark`;
