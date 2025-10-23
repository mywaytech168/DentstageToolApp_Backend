-- 新增估價單與維修單的分類折扣欄位，記錄凹痕、板烤與其他類別的額外費用與折扣趴數。
ALTER TABLE Quatations
    ADD COLUMN DentOtherFee DECIMAL(10,2) NULL AFTER Discount_reason,
    ADD COLUMN DentPercentageDiscount DECIMAL(5,2) NULL AFTER DentOtherFee,
    ADD COLUMN PaintOtherFee DECIMAL(10,2) NULL AFTER DentPercentageDiscount,
    ADD COLUMN PaintPercentageDiscount DECIMAL(5,2) NULL AFTER PaintOtherFee,
    ADD COLUMN OtherOtherFee DECIMAL(10,2) NULL AFTER PaintPercentageDiscount,
    ADD COLUMN OtherPercentageDiscount DECIMAL(5,2) NULL AFTER OtherOtherFee;

-- 新增維修單對應欄位，確保估價與維修流程共用相同的分類金額資訊。
ALTER TABLE Orders
    ADD COLUMN DentOtherFee DECIMAL(10,2) NULL AFTER Discount_reason,
    ADD COLUMN DentPercentageDiscount DECIMAL(5,2) NULL AFTER DentOtherFee,
    ADD COLUMN PaintOtherFee DECIMAL(10,2) NULL AFTER DentPercentageDiscount,
    ADD COLUMN PaintPercentageDiscount DECIMAL(5,2) NULL AFTER PaintOtherFee,
    ADD COLUMN OtherOtherFee DECIMAL(10,2) NULL AFTER PaintPercentageDiscount,
    ADD COLUMN OtherPercentageDiscount DECIMAL(5,2) NULL AFTER OtherOtherFee;
