using System;
using System.Collections.Generic;

namespace DentstageToolApp.Infrastructure.Entities;

/// <summary>
/// 報價單資料實體，對應資料庫 Quatations 資料表。
/// </summary>
public class Quatation
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
    /// 報價單唯一識別碼。
    /// </summary>
    public string QuotationUid { get; set; } = null!;

    /// <summary>
    /// 報價單編號。
    /// </summary>
    public string? QuotationNo { get; set; }

    /// <summary>
    /// 報價單序號。
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
    /// 導覽屬性：門市主檔資料。
    /// </summary>
    public Store? StoreNavigation { get; set; }

    /// <summary>
    /// 建立估價單的估價技師識別碼（UID）。
    /// </summary>
    public string? EstimationTechnicianUid { get; set; }

    /// <summary>
    /// 導覽屬性：估價技師主檔資料。
    /// </summary>
    public Technician? EstimationTechnicianNavigation { get; set; }

    /// <summary>
    /// 製單技師識別碼，預留與舊系統同步使用。
    /// </summary>
    public string? CreatorTechnicianUid { get; set; }

    /// <summary>
    /// 報價日期。
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 報價單狀態。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 維修類型鍵值，使用 dent、beauty、paint、other 或舊版中文描述。
    /// </summary>
    public string? FixType { get; set; }

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
    /// 車牌。
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
    /// 車輛品牌識別碼（UID），對應 Brands 表格主鍵。
    /// </summary>
    public string? BrandUid { get; set; }

    /// <summary>
    /// 車輛型號識別碼（UID），對應 Models 表格主鍵。
    /// </summary>
    public string? ModelUid { get; set; }

    /// <summary>
    /// 導覽屬性：報價單所關聯的品牌資料。
    /// </summary>
    public Brand? BrandNavigation { get; set; }

    /// <summary>
    /// 導覽屬性：報價單所關聯的車型資料。
    /// </summary>
    public Model? ModelNavigation { get; set; }

    /// <summary>
    /// 車色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 車輛備註。
    /// </summary>
    public string? CarRemark { get; set; }

    /// <summary>
    /// 車輛里程數，以公里數記錄估價當下資料。
    /// </summary>
    public int? Milage { get; set; }

    /// <summary>
    /// 品牌型號顯示字串。
    /// </summary>
    public string? BrandModel { get; set; }

    /// <summary>
    /// 顧客識別碼。
    /// </summary>
    public string? CustomerUid { get; set; }

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
    /// 顧客類型。
    /// </summary>
    public string? CustomerType { get; set; }

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
    /// 電子郵件。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 詢問原因。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 聯絡備註。
    /// </summary>
    public string? ConnectRemark { get; set; }

    /// <summary>
    /// 估價金額。
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
    /// 預約日期。
    /// </summary>
    public DateOnly? BookDate { get; set; }

    /// <summary>
    /// 預約方式。
    /// </summary>
    public string? BookMethod { get; set; }

    /// <summary>
    /// 代步車需求。
    /// </summary>
    public string? CarReserved { get; set; }

    /// <summary>
    /// 預計施工日期。
    /// </summary>
    public DateOnly? FixDate { get; set; }

    /// <summary>
    /// 工具測試資訊。
    /// </summary>
    public string? ToolTest { get; set; }

    /// <summary>
    /// 鍍膜資訊。
    /// </summary>
    public string? Coat { get; set; }

    /// <summary>
    /// 信封資訊。
    /// </summary>
    public string? Envelope { get; set; }

    /// <summary>
    /// 烤漆資訊。
    /// </summary>
    public string? Paint { get; set; }

    /// <summary>
    /// 備註。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 110 狀態時間戳記。
    /// </summary>
    public DateTime? Status110Timestamp { get; set; }

    /// <summary>
    /// 110 狀態處理人。
    /// </summary>
    public string? Status110User { get; set; }

    /// <summary>
    /// 180 狀態時間戳記。
    /// </summary>
    public DateTime? Status180Timestamp { get; set; }

    /// <summary>
    /// 180 狀態處理人。
    /// </summary>
    public string? Status180User { get; set; }

    /// <summary>
    /// 190 狀態時間戳記。
    /// </summary>
    public DateTime? Status190Timestamp { get; set; }

    /// <summary>
    /// 190 狀態處理人。
    /// </summary>
    public string? Status190User { get; set; }

    /// <summary>
    /// 191 狀態時間戳記。
    /// </summary>
    public DateTime? Status191Timestamp { get; set; }

    /// <summary>
    /// 191 狀態處理人。
    /// </summary>
    public string? Status191User { get; set; }

    /// <summary>
    /// 199 狀態時間戳記。
    /// </summary>
    public DateTime? Status199Timestamp { get; set; }

    /// <summary>
    /// 199 狀態處理人。
    /// </summary>
    public string? Status199User { get; set; }

    /// <summary>
    /// 目前狀態更新時間。
    /// </summary>
    public DateTime? CurrentStatusDate { get; set; }

    /// <summary>
    /// 目前狀態更新人。
    /// </summary>
    public string? CurrentStatusUser { get; set; }

    /// <summary>
    /// 修復預期描述。
    /// </summary>
    public string? FixExpect { get; set; }

    /// <summary>
    /// 拒絕標記。
    /// </summary>
    public bool? Reject { get; set; }

    /// <summary>
    /// 拒絕原因。
    /// </summary>
    public string? RejectReason { get; set; }

    /// <summary>
    /// 板金需求。
    /// </summary>
    public string? PanelBeat { get; set; }

    /// <summary>
    /// 板金需求原因。
    /// </summary>
    public string? PanelBeatReason { get; set; }

    /// <summary>
    /// 修復工時（小時）。
    /// </summary>
    public int? FixTimeHour { get; set; }

    /// <summary>
    /// 修復工時（分鐘）。
    /// </summary>
    public int? FixTimeMin { get; set; }

    /// <summary>
    /// 修復預期天數。
    /// </summary>
    public int? FixExpectDay { get; set; }

    /// <summary>
    /// 修復預期小時。
    /// </summary>
    public int? FixExpectHour { get; set; }

    /// <summary>
    /// 是否為常客。
    /// </summary>
    public bool? FlagRegularCustomer { get; set; }

    /// <summary>
    /// 關聯顧客導航屬性。
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// 關聯車輛導航屬性。
    /// </summary>
    public Car? Car { get; set; }

    /// <summary>
    /// 關聯工單清單。
    /// </summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    /// <summary>
    /// 關聯美容服務設定。
    /// </summary>
    public CarBeauty? CarBeauty { get; set; }

    /// <summary>
    /// 關聯照片資訊清單。
    /// </summary>
    public ICollection<PhotoDatum> PhotoData { get; set; } = new List<PhotoDatum>();
}
