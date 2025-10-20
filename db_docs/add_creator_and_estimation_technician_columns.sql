-- 新增估價單與維修單串接欄位，補齊估價技師與製單技師資料。
ALTER TABLE Quatations
    ADD COLUMN EstimationTechnicianUID VARCHAR(100) NULL AFTER UserName,
    ADD COLUMN CreatorTechnicianUID VARCHAR(100) NULL AFTER EstimationTechnicianUID;

-- 以既有估價資料回填估價技師欄位，維持舊資料可用性。
UPDATE Quatations
SET EstimationTechnicianUID = UserUID
WHERE EstimationTechnicianUID IS NULL AND UserUID IS NOT NULL;

-- 新增維修單估價技師與製單技師欄位，與估價單欄位保持一致。
ALTER TABLE Orders
    ADD COLUMN EstimationTechnicianUID VARCHAR(100) NULL AFTER UserName,
    ADD COLUMN CreatorTechnicianUID VARCHAR(100) NULL AFTER EstimationTechnicianUID;

-- 依估價單資料回填維修單估價技師欄位，優先使用估價技師 UID，其次為舊版使用者 UID。
UPDATE Orders o
LEFT JOIN Quatations q ON o.QuatationUID = q.QuotationUID
SET o.EstimationTechnicianUID = COALESCE(o.EstimationTechnicianUID, q.EstimationTechnicianUID, q.UserUID)
WHERE COALESCE(o.EstimationTechnicianUID, q.EstimationTechnicianUID, q.UserUID) IS NOT NULL;

-- 若估價單已具備製單技師，則同步補齊維修單資料，若缺少則沿用估價技師 UID。
UPDATE Orders o
LEFT JOIN Quatations q ON o.QuatationUID = q.QuotationUID
SET o.CreatorTechnicianUID = COALESCE(o.CreatorTechnicianUID, q.CreatorTechnicianUID, q.EstimationTechnicianUID, q.UserUID)
WHERE COALESCE(o.CreatorTechnicianUID, q.CreatorTechnicianUID, q.EstimationTechnicianUID, q.UserUID) IS NOT NULL;
