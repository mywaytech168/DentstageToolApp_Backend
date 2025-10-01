namespace DentstageToolApp.Api.CarPlates;

/// <summary>
/// 車牌維修紀錄查詢請求資料，封裝前端輸入的車牌號碼。
/// </summary>
public class CarPlateMaintenanceHistoryRequest
{
    /// <summary>
    /// 欲查詢的車牌號碼。
    /// </summary>
    public string CarPlateNumber { get; set; } = string.Empty;
}
