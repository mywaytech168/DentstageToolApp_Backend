namespace DentstageToolApp.Api.Services.Safety;

/// <summary>
/// 安全警告分類，對應不同場域的異常情境。
/// </summary>
public enum SafetyAlertType
{
    /// <summary>
    /// 無掛勾無人（門模式）警告，代表工區門未掛勾且附近無人員。
    /// </summary>
    DoorModeNoHookNoPerson = 1,

    /// <summary>
    /// 無背心無頭盔（監控模式）警告，代表監控畫面未偵測到必要的安全配備。
    /// </summary>
    MonitorModeNoVestNoHelmet = 2
}
