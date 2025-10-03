using System;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價單轉維修後回傳的資訊，包含新建工單資料。
/// </summary>
public class QuotationMaintenanceConversionResponse : QuotationStatusChangeResponse
{
    /// <summary>
    /// 新建維修單的唯一識別碼。
    /// </summary>
    public string OrderUid { get; set; } = string.Empty;

    /// <summary>
    /// 新建維修單編號，供前端顯示與跳轉使用。
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 維修單建立時間。
    /// </summary>
    public DateTime OrderCreatedAt { get; set; }
}
