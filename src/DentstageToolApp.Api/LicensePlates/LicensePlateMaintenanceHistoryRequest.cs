namespace DentstageToolApp.Api.LicensePlates;

/// <summary>
/// 車牌維修紀錄查詢請求資料，封裝前端輸入的車牌號碼。
/// </summary>
public class LicensePlateMaintenanceHistoryRequest
{
    /// <summary>
    /// 欲查詢的車牌號碼。
    /// </summary>
    public string LicensePlateNumber { get; set; } = string.Empty;
}
