using System;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.MaintenanceOrders;

/// <summary>
/// 維修單退傭請求結構，提供密碼驗證與退傭金額。
/// </summary>
public class MaintenanceOrderRebateRequest
{
    /// <summary>
    /// 維修單編號，必須填寫以定位工單。
    /// </summary>
    [Required(ErrorMessage = "請提供維修單編號。")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 操作密碼，需通過後端驗證才能進行退傭。
    /// </summary>
    [Required(ErrorMessage = "請輸入退傭密碼。")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 本次退傭金額，單位為新台幣，允許填寫 0 代表不退傭。
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "退傭金額不可為負值。")]
    public decimal RebateAmount { get; set; }
}

/// <summary>
/// 維修單退傭回應，回傳實際金額與扣除退傭後的淨額。
/// </summary>
public class MaintenanceOrderRebateResponse
{
    /// <summary>
    /// 維修單編號。
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 退傭後的淨收金額。
    /// </summary>
    public decimal? NetAmount { get; set; }

    /// <summary>
    /// 本次計算的實際維修金額。
    /// </summary>
    public decimal? ActualAmount { get; set; }

    /// <summary>
    /// 本次設定的退傭金額。
    /// </summary>
    public decimal RebateAmount { get; set; }

    /// <summary>
    /// 操作時間，使用台北時區。
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
