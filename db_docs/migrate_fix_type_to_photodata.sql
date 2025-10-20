-- 將照片與估價單的維修類型欄位調整為儲存固定鍵值（dent、beauty、paint、other）。
ALTER TABLE PhotoData
    MODIFY COLUMN Fix_Type VARCHAR(100) NULL;

ALTER TABLE Quatations
    MODIFY COLUMN Fix_Type VARCHAR(100) NULL;

-- 先將估價單的維修類型正規化為固定鍵值，預設未辨識的資料歸類為 other。
UPDATE Quatations
SET Fix_Type = CASE
    WHEN Fix_Type IS NULL OR Fix_Type = '' THEN NULL
    WHEN LOWER(Fix_Type) IN ('dent', '凹痕', 'dentrepair') THEN 'dent'
    WHEN LOWER(Fix_Type) IN ('beauty', '美容') THEN 'beauty'
    WHEN LOWER(Fix_Type) IN ('paint', '鈑烤', '板烤', '烤漆') THEN 'paint'
    ELSE 'other'
END;

-- 將照片維修欄位正規化並對齊估價單的設定。
UPDATE PhotoData
SET Fix_Type = CASE
    WHEN Fix_Type IS NULL OR Fix_Type = '' THEN NULL
    WHEN LOWER(Fix_Type) IN ('dent', '凹痕', 'dentrepair') THEN 'dent'
    WHEN LOWER(Fix_Type) IN ('beauty', '美容') THEN 'beauty'
    WHEN LOWER(Fix_Type) IN ('paint', '鈑烤', '板烤', '烤漆') THEN 'paint'
    ELSE 'other'
END;

-- 若照片尚未指定維修類型，則沿用估價單的分類結果。
UPDATE PhotoData AS pd
INNER JOIN Quatations AS q ON q.QuotationUID = pd.QuotationUID
SET pd.Fix_Type = q.Fix_Type
WHERE (pd.Fix_Type IS NULL OR pd.Fix_Type = '')
  AND q.Fix_Type IS NOT NULL AND q.Fix_Type <> '';

-- 調整 fix_types 結構，改以 FixType 為主鍵並重建標準分類。
ALTER TABLE fix_types
    CHANGE COLUMN FixTypeUID FixType VARCHAR(50) NOT NULL;

ALTER TABLE fix_types
    DROP PRIMARY KEY,
    ADD PRIMARY KEY (FixType);

TRUNCATE TABLE fix_types;

INSERT INTO fix_types (FixType, FixTypeName) VALUES
    ('dent', '凹痕'),
    ('beauty', '美容'),
    ('paint', '鈑烤'),
    ('other', '其他')
ON DUPLICATE KEY UPDATE FixTypeName = VALUES(FixTypeName);
