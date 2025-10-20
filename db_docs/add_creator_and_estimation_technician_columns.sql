-- 新增估價單與維修單串接欄位，補齊估價技師與製單技師資料。
ALTER TABLE Quatations
    ADD COLUMN EstimationTechnicianUID VARCHAR(100) NULL AFTER UserName,
    ADD COLUMN CreatorTechnicianUID VARCHAR(100) NULL AFTER EstimationTechnicianUID;

-- 以既有估價資料回填估價技師欄位，維持舊資料可用性。
UPDATE Quatations
SET EstimationTechnicianUID = UserUID
WHERE EstimationTechnicianUID IS NULL AND UserUID IS NOT NULL;

-- 新增維修單的製單技師欄位，供舊新系統同步使用。
ALTER TABLE Orders
    ADD COLUMN CreatorTechnicianUID VARCHAR(100) NULL AFTER UserName;

-- 若估價單已具備製單技師，則同步補齊維修單資料。
UPDATE Orders o
LEFT JOIN Quatations q ON o.QuatationUID = q.QuotationUID
SET o.CreatorTechnicianUID = COALESCE(o.CreatorTechnicianUID, q.CreatorTechnicianUID)
WHERE COALESCE(o.CreatorTechnicianUID, q.CreatorTechnicianUID) IS NOT NULL;
