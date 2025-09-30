using System;

namespace DentstageToolApp.Api.CarPlates;

/// <summary>
/// 車牌維修紀錄項目，描述每一筆維修工單的重點資訊。
/// </summary>
public class CarPlateMaintenanceRecordDto
{
    /// <summary>
    /// 工單唯一識別碼，便於前端導向詳細資料頁面。
    /// </summary>
    public string OrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 工單編號。
    /// </summary>
    public string? OrderNo { get; set; }

    /// <summary>
    /// 工單日期。
    /// </summary>
    public DateOnly? OrderDate { get; set; }

    /// <summary>
    /// 工單建立時間，用於展示建立流程或排序備援。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 工單狀態。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 維修類型。
    /// </summary>
    public string? FixType { get; set; }

    /// <summary>
    /// 維修金額。
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// 排程開工日期文字描述，沿用資料庫原始欄位。
    /// </summary>
    public string? WorkDate { get; set; }

    /// <summary>
    /// 工單備註，用於顯示維修說明。
    /// </summary>
    public string? Remark { get; set; }
}
