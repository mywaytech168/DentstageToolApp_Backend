using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 取消估價單或預約的請求內容，提供取消原因。
/// </summary>
public class QuotationCancelRequest : QuotationActionRequestBase
{
    /// <summary>
    /// 取消原因，若未填寫將於服務層套用預設說明。
    /// </summary>
    [MaxLength(255, ErrorMessage = "取消原因長度不可超過 255 個字元")]
    public string? Reason { get; set; }

    /// <summary>
    /// 取消後是否需清除預約日期，預設僅取消估價單不移除預約資訊。
    /// </summary>
    public bool ClearReservation { get; set; }
}
