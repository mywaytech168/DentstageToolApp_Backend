using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 編輯估價單所需的欄位，聚焦車輛、客戶與各類別備註。
/// </summary>
public class UpdateQuotationRequest : QuotationActionRequestBase
{

    /// <summary>
    /// 車輛資訊。
    /// </summary>
    public QuotationCarInfo Car { get; set; } = new();

    /// <summary>
    /// 客戶資訊。
    /// </summary>
    public QuotationCustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// 店家與排程資訊，沿用建立估價單時的欄位以便前端共用表單。
    /// </summary>
    public QuotationStoreUpdateInfo Store { get; set; } = new();

    /// <summary>
    /// 各類別整體備註，key 使用 dent、paint、other。
    /// </summary>
    public Dictionary<string, string?> CategoryRemarks { get; set; } = new();

    /// <summary>
    /// 若需要同步更新估價單整體備註可一併傳入。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 維修照片集合，與建立估價單相同以四大類別區分。
    /// </summary>
    public QuotationPhotoRequestCollection Photos { get; set; } = new();

    /// <summary>
    /// 車體確認單資料，包含標記與簽名資訊。
    /// </summary>
    public QuotationCarBodyConfirmation? CarBodyConfirmation { get; set; }

    /// <summary>
    /// 維修設定資訊，與詳情回傳欄位同步，方便前端直接提交完整資料。
    /// </summary>
    public QuotationMaintenanceInfo? Maintenance { get; set; }
}

/// <summary>
/// 編輯估價單時可更新的店家資訊欄位，方便同步技師與排程設定。
/// </summary>
public class QuotationStoreUpdateInfo
{
    /// <summary>
    /// 估價技師識別碼（UID），改派時需一併更新門市與估價人員名稱。
    /// </summary>
    public string? EstimationTechnicianUid { get; set; }

    /// <summary>
    /// 製單技師識別碼（UID），允許獨立指定與估價技師不同的人員。
    /// </summary>
    public string? CreatorTechnicianUid { get; set; }

    /// <summary>
    /// 維修來源，沿用建立估價單欄位。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 預約方式，沿用建立估價單欄位，供前端同步顯示。
    /// </summary>
    public string? BookMethod { get; set; }

    /// <summary>
    /// 預約日期，僅在提供值時才會更新資料庫欄位。
    /// </summary>
    public DateTime? ReservationDate { get; set; }

    /// <summary>
    /// 預計維修日期，僅在提供值時才會更新資料庫欄位。
    /// </summary>
    public DateTime? RepairDate { get; set; }
}

