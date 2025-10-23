using System;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 工單資料實體，對應資料庫 Orders 資料表。
/// </summary>
public class Order
{
    /// <summary>
    /// 建立時間戳記。
    /// </summary>
    public DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// 建立人員。
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改時間戳記。
    /// </summary>
    public DateTime? ModificationTimestamp { get; set; }

    /// <summary>
    /// 修改人員。
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 工單唯一識別碼。
    /// </summary>
    public string OrderUid { get; set; } = null!;

    /// <summary>
    /// 工單編號。
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    /// 工單序號。
    /// </summary>
    public int? SerialNum { get; set; }

    /// <summary>
    /// 門市識別碼。
    /// </summary>
    public string? StoreUid { get; set; }

    /// <summary>
    /// 建立使用者識別碼。
    /// </summary>
    public string? UserUid { get; set; }

    /// <summary>
    /// 建立使用者名稱。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 估價技師識別碼，保留維修單建立時的估價人員資訊。
    /// </summary>
    public string? EstimationTechnicianUid { get; set; }

    /// <summary>
    /// 製單技師識別碼，提供與估價單一致的技師資訊。
    /// </summary>
    public string? CreatorTechnicianUid { get; set; }

    /// <summary>
    /// 工單日期。
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 工單狀態。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 關聯車輛識別碼。
    /// </summary>
    public string? CarUid { get; set; }

    /// <summary>
    /// 原始輸入車牌（含國碼）。
    /// </summary>
    public string? CarNoInputGlobal { get; set; }

    /// <summary>
    /// 原始輸入車牌。
    /// </summary>
    public string? CarNoInput { get; set; }

    /// <summary>
    /// 最終車牌。
    /// </summary>
    public string? CarNo { get; set; }

    /// <summary>
    /// 車輛品牌。
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// 車輛型號。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? CarRemark { get; set; }

    /// <summary>
    /// 品牌型號顯示字串。
    /// </summary>
    public string? BrandModel { get; set; }

    /// <summary>
    /// 顧客識別碼。
    /// </summary>
    public string? CustomerUid { get; set; }

    /// <summary>
    /// 顧客類型。
    /// </summary>
    public string? CustomerType { get; set; }

    /// <summary>
    /// 電話輸入（含國碼）。
    /// </summary>
    public string? PhoneInputGlobal { get; set; }

    /// <summary>
    /// 電話輸入。
    /// </summary>
    public string? PhoneInput { get; set; }

    /// <summary>
    /// 電話。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 顧客姓名。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 性別。
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 聯絡方式。
    /// </summary>
    public string? Connect { get; set; }

    /// <summary>
    /// 縣市。
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// 鄉鎮區。
    /// </summary>
    public string? Township { get; set; }

    /// <summary>
    /// 來源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 詢問原因。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 電子郵件。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 聯絡備註。
    /// </summary>
    public string? ConnectRemark { get; set; }

    /// <summary>
    /// 關聯報價單識別碼。
    /// </summary>
    public string? QuatationUid { get; set; }

    /// <summary>
    /// 預約日期（以字串儲存，方便對應 MySQL VARCHAR 欄位）。
    /// </summary>
    public string? BookDate { get; set; }

    /// <summary>
    /// 預約方式。
    /// </summary>
    public string? BookMethod { get; set; }

    /// <summary>
    /// 預計施工日期（以字串儲存，保留原始填寫格式）。
    /// </summary>
    public string? WorkDate { get; set; }

    /// <summary>
    /// 施工日期備註。
    /// </summary>
    public string? WorkDateRemark { get; set; }

    /// <summary>
    /// 維修類型。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 施工內容描述。
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 是否代步車需求。
    /// </summary>
    public string? CarReserved { get; set; }

    /// <summary>
    /// 里程數。
    /// </summary>
    public int? Milage { get; set; }

    /// <summary>
    /// 工單備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 210 狀態時間戳記。
    /// </summary>
    public DateTime? Status210Date { get; set; }

    /// <summary>
    /// 210 狀態處理人。
    /// </summary>
    public string? Status210User { get; set; }

    /// <summary>
    /// 220 狀態時間戳記。
    /// </summary>
    public DateTime? Status220Date { get; set; }

    /// <summary>
    /// 220 狀態處理人。
    /// </summary>
    public string? Status220User { get; set; }

    /// <summary>
    /// 290 狀態時間戳記。
    /// </summary>
    public DateTime? Status290Date { get; set; }

    /// <summary>
    /// 290 狀態處理人。
    /// </summary>
    public string? Status290User { get; set; }

    /// <summary>
    /// 295 狀態時間戳記。
    /// </summary>
    public DateTime? Status295Timestamp { get; set; }

    /// <summary>
    /// 295 狀態處理人。
    /// </summary>
    public string? Status295User { get; set; }

    /// <summary>
    /// 作業紀錄識別碼。
    /// </summary>
    public string? WorkRecordUid { get; set; }

    /// <summary>
    /// 目前狀態更新時間。
    /// </summary>
    public DateTime? CurrentStatusDate { get; set; }

    /// <summary>
    /// 目前狀態更新人。
    /// </summary>
    public string? CurrentStatusUser { get; set; }

    /// <summary>
    /// 簽名異動時間。
    /// </summary>
    public DateTime? SignatureModifyTimestamp { get; set; }

    /// <summary>
    /// 原始估價金額。
    /// </summary>
    public decimal? Valuation { get; set; }

    /// <summary>
    /// 折扣百分比。
    /// </summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>
    /// 折扣金額。
    /// </summary>
    public decimal? Discount { get; set; }

    /// <summary>
    /// 折扣原因。
    /// </summary>
    public string? DiscountReason { get; set; }

    /// <summary>
    /// 凹痕類別的其他費用。
    /// </summary>
    public decimal? DentOtherFee { get; set; }

    /// <summary>
    /// 凹痕類別的折扣百分比。
    /// </summary>
    public decimal? DentPercentageDiscount { get; set; }

    /// <summary>
    /// 板烤類別的其他費用。
    /// </summary>
    public decimal? PaintOtherFee { get; set; }

    /// <summary>
    /// 板烤類別的折扣百分比。
    /// </summary>
    public decimal? PaintPercentageDiscount { get; set; }

    /// <summary>
    /// 其他類別的其他費用。
    /// </summary>
    public decimal? OtherOtherFee { get; set; }

    /// <summary>
    /// 其他類別的折扣百分比。
    /// </summary>
    public decimal? OtherPercentageDiscount { get; set; }

    /// <summary>
    /// 應付金額。
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// 停止原因。
    /// </summary>
    public string? StopReason { get; set; }

    /// <summary>
    /// 回饋金額。
    /// </summary>
    public decimal? Rebate { get; set; }

    /// <summary>
    /// 是否為常客。
    /// </summary>
    public bool? FlagRegularCustomer { get; set; }

    /// <summary>
    /// 是否為外部合作案件。
    /// </summary>
    public bool? FlagExternalCooperation { get; set; }

    /// <summary>
    /// 關聯報價單導航屬性。
    /// </summary>
    public Quatation? Quatation { get; set; }

    /// <summary>
    /// 關聯顧客導航屬性。
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// 關聯車輛導航屬性。
    /// </summary>
    public Car? Car { get; set; }
}
