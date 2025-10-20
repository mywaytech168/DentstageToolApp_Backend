-- 新增照片維修類型欄位並搬移舊資料，確保多維修類型可同時存在。
ALTER TABLE PhotoData
    ADD COLUMN Fix_Type VARCHAR(50) NULL;

-- 將估價單上的 Fix_Type 舊欄位回填到照片資料，僅在照片尚未設定時更新。
UPDATE PhotoData AS pd
INNER JOIN Quatations AS q ON q.QuotationUID = pd.QuotationUID
SET pd.Fix_Type = COALESCE(NULLIF(pd.Fix_Type, ''), q.Fix_Type)
WHERE (pd.Fix_Type IS NULL OR pd.Fix_Type = '')
  AND q.Fix_Type IS NOT NULL AND q.Fix_Type <> '';

-- 若舊資料缺少對應照片，可保留 Quatations.Fix_Type 作為相容欄位。
