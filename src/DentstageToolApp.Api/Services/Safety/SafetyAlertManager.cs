using System;
using System.Collections.Generic;

namespace DentstageToolApp.Api.Services.Safety;

/// <summary>
/// 安全警告管理器，負責處理異常/復歸的重送節奏，避免頻繁發送通知。
/// </summary>
public class SafetyAlertManager
{
    // ---------- 欄位與屬性區 ----------

    /// <summary>
    /// 重新發送的預設時間間隔（1 分鐘）。
    /// </summary>
    public static readonly TimeSpan DefaultResendInterval = TimeSpan.FromMinutes(1);

    private readonly Dictionary<SafetyAlertType, SafetyAlertState> _states;
    private readonly TimeSpan _resendInterval;

    // ---------- 建構子區 ----------

    /// <summary>
    /// 建立使用預設重送間隔的管理器實例。
    /// </summary>
    public SafetyAlertManager()
        : this(DefaultResendInterval)
    {
    }

    /// <summary>
    /// 建立安全警告管理器，可自訂重送時間間隔。
    /// </summary>
    /// <param name="resendInterval">重新發送通知的間隔時間。</param>
    /// <exception cref="ArgumentOutOfRangeException">間隔時間必須大於 0。</exception>
    public SafetyAlertManager(TimeSpan resendInterval)
    {
        if (resendInterval <= TimeSpan.Zero)
        {
            // 間隔時間若不合理，提前阻擋避免造成 CPU Busy Loop 或太頻繁的通知。
            throw new ArgumentOutOfRangeException(nameof(resendInterval), "重送間隔必須大於零。");
        }

        _resendInterval = resendInterval;
        _states = new Dictionary<SafetyAlertType, SafetyAlertState>();

        foreach (var alertType in Enum.GetValues<SafetyAlertType>())
        {
            // 預先建立狀態物件，避免後續查詢時需要再初始化。
            _states[alertType] = new SafetyAlertState(alertType);
        }
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 判斷指定警告是否需要發送通知，並更新狀態。
    /// </summary>
    /// <param name="alertType">警告種類。</param>
    /// <param name="isAbnormal">目前是否處於異常狀態。</param>
    /// <param name="timestamp">呼叫時間，預設使用 UTC 時間戳記。</param>
    /// <param name="notificationKind">若需發送通知，回傳對應通知類型。</param>
    /// <returns>若需要對外發送通知則回傳 <c>true</c>。</returns>
    public bool TryGetNotification(
        SafetyAlertType alertType,
        bool isAbnormal,
        DateTimeOffset timestamp,
        out SafetyAlertNotificationKind notificationKind)
    {
        var state = _states[alertType];

        if (isAbnormal)
        {
            // 第一次進入異常，需立即告警。
            if (!state.IsAbnormal)
            {
                state.MarkWarning(timestamp);
                notificationKind = SafetyAlertNotificationKind.Warning;
                return true;
            }

            // 持續異常時依據間隔重新發送，避免過於頻繁。
            if (state.ShouldResendWarning(timestamp, _resendInterval))
            {
                state.MarkWarning(timestamp);
                notificationKind = SafetyAlertNotificationKind.Warning;
                return true;
            }

            notificationKind = default;
            return false;
        }

        // 未曾發生異常且目前為正常狀態，不需額外通知。
        if (!state.HasTriggeredWarning && !state.IsAbnormal)
        {
            notificationKind = default;
            return false;
        }

        // 異常剛排除，需立即發送復歸通知。
        if (state.IsAbnormal)
        {
            state.MarkRecovery(timestamp);
            notificationKind = SafetyAlertNotificationKind.Recovery;
            return true;
        }

        // 保持正常狀態時，每分鐘主動補發復歸，確保前端同步。
        if (state.ShouldResendRecovery(timestamp, _resendInterval))
        {
            state.MarkRecovery(timestamp);
            notificationKind = SafetyAlertNotificationKind.Recovery;
            return true;
        }

        notificationKind = default;
        return false;
    }

    /// <summary>
    /// 取得指定警告類型的狀態快照，方便除錯或顯示於監控介面。
    /// </summary>
    public SafetyAlertSnapshot GetSnapshot(SafetyAlertType alertType)
    {
        var state = _states[alertType];
        return state.ToSnapshot();
    }

    /// <summary>
    /// 輸出所有警告類型的狀態，供儀表板或診斷記錄使用。
    /// </summary>
    public IReadOnlyCollection<SafetyAlertSnapshot> ExportSnapshots()
    {
        var results = new List<SafetyAlertSnapshot>(_states.Count);

        foreach (var state in _states.Values)
        {
            results.Add(state.ToSnapshot());
        }

        return results;
    }

    // ---------- 內部類別區 ----------

    private sealed class SafetyAlertState
    {
        /// <summary>
        /// 建立狀態物件並對應警告種類。
        /// </summary>
        public SafetyAlertState(SafetyAlertType alertType)
        {
            AlertType = alertType;
        }

        /// <summary>
        /// 警告種類，協助除錯時辨識來源。
        /// </summary>
        public SafetyAlertType AlertType { get; }

        /// <summary>
        /// 是否處於異常狀態。
        /// </summary>
        public bool IsAbnormal { get; private set; }

        /// <summary>
        /// 是否曾發送過異常通知，避免初始正常狀態就自動復歸。
        /// </summary>
        public bool HasTriggeredWarning { get; private set; }

        /// <summary>
        /// 最近一次發送異常通知的時間。
        /// </summary>
        public DateTimeOffset? LastWarningSentAt { get; private set; }

        /// <summary>
        /// 最近一次發送復歸通知的時間。
        /// </summary>
        public DateTimeOffset? LastRecoverySentAt { get; private set; }

        /// <summary>
        /// 異常狀態已確認，需要更新旗標與時間戳記。
        /// </summary>
        public void MarkWarning(DateTimeOffset timestamp)
        {
            IsAbnormal = true;
            HasTriggeredWarning = true;
            LastWarningSentAt = timestamp;
        }

        /// <summary>
        /// 狀態已恢復正常，更新旗標與時間戳記。
        /// </summary>
        public void MarkRecovery(DateTimeOffset timestamp)
        {
            IsAbnormal = false;
            LastRecoverySentAt = timestamp;
        }

        /// <summary>
        /// 判斷是否需重新發送異常通知。
        /// </summary>
        public bool ShouldResendWarning(DateTimeOffset timestamp, TimeSpan interval)
        {
            if (!LastWarningSentAt.HasValue)
            {
                return true;
            }

            return timestamp - LastWarningSentAt.Value >= interval;
        }

        /// <summary>
        /// 判斷是否需重新發送復歸通知。
        /// </summary>
        public bool ShouldResendRecovery(DateTimeOffset timestamp, TimeSpan interval)
        {
            if (!LastRecoverySentAt.HasValue)
            {
                return true;
            }

            return timestamp - LastRecoverySentAt.Value >= interval;
        }

        /// <summary>
        /// 轉換為只讀快照，提供外部呼叫端檢視。
        /// </summary>
        public SafetyAlertSnapshot ToSnapshot()
        {
            return new SafetyAlertSnapshot(
                AlertType,
                IsAbnormal,
                LastWarningSentAt,
                LastRecoverySentAt,
                HasTriggeredWarning);
        }
    }
}

/// <summary>
/// 安全警告狀態的對外快照，協助監控頁面呈現資訊。
/// </summary>
/// <param name="AlertType">警告類型。</param>
/// <param name="IsAbnormal">目前是否為異常狀態。</param>
/// <param name="LastWarningSentAt">最近一次發送異常通知的時間。</param>
/// <param name="LastRecoverySentAt">最近一次發送復歸通知的時間。</param>
/// <param name="HasTriggeredWarning">是否曾觸發過異常通知。</param>
public record SafetyAlertSnapshot(
    SafetyAlertType AlertType,
    bool IsAbnormal,
    DateTimeOffset? LastWarningSentAt,
    DateTimeOffset? LastRecoverySentAt,
    bool HasTriggeredWarning);
