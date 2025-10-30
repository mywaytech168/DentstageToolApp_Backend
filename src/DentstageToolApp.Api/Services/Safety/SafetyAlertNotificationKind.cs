namespace DentstageToolApp.Api.Services.Safety;

/// <summary>
/// 安全警告訊息類型，區分異常與復歸狀態。
/// </summary>
public enum SafetyAlertNotificationKind
{
    /// <summary>
    /// 表示觸發異常狀態，需通知現場即時處理。
    /// </summary>
    Warning = 1,

    /// <summary>
    /// 表示狀態已回復正常，需通知前端或看板進行復歸。
    /// </summary>
    Recovery = 2
}
