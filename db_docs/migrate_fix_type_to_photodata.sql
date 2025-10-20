-- 將照片維修欄位調整為儲存 FixTypeUID，並將既有資料對應到維修主檔。
ALTER TABLE PhotoData
    MODIFY COLUMN Fix_Type VARCHAR(100) NULL;

-- 先將舊資料中以名稱或 UID 儲存的資料統一轉換成 FixTypeUID。
UPDATE PhotoData AS pd
LEFT JOIN FixTypes AS ft_uid ON ft_uid.FixTypeUid = pd.Fix_Type
LEFT JOIN FixTypes AS ft_name ON ft_name.FixTypeName = pd.Fix_Type
SET pd.Fix_Type = COALESCE(ft_uid.FixTypeUid, ft_name.FixTypeUid, pd.Fix_Type)
WHERE pd.Fix_Type IS NOT NULL AND pd.Fix_Type <> '';

-- 將估價單上的維修類型回填到照片，優先使用維修主檔取得的 UID。
UPDATE PhotoData AS pd
INNER JOIN Quatations AS q ON q.QuotationUID = pd.QuotationUID
LEFT JOIN FixTypes AS ft ON ft.FixTypeUid = q.Fix_Type OR ft.FixTypeName = q.Fix_Type
SET pd.Fix_Type = COALESCE(NULLIF(pd.Fix_Type, ''), ft.FixTypeUid, q.Fix_Type)
WHERE (pd.Fix_Type IS NULL OR pd.Fix_Type = '')
  AND q.Fix_Type IS NOT NULL AND q.Fix_Type <> '';

-- 若舊系統仍依賴 Quatations.Fix_Type，請保留原欄位以維持相容性。
