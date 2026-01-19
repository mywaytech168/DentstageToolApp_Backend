using System;
using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Quotations;

/// <summary>
/// 轉預約或設定預約日期的請求內容。
/// </summary>
public class QuotationReservationRequest : QuotationActionRequestBase
{
    /// <summary>
    /// 指定的預約日期，服務層會轉換為 DateOnly 儲存至 ReservationFixDate 欄位。
    /// </summary>
    [Required(ErrorMessage = "請提供預約日期")]
    public DateTime? ReservationDate { get; set; }

    /// <summary>
    /// 預約內容，例如預約要求、特殊說明或備註。此欄位為選用。
    /// </summary>
    public string? ReservationContent { get; set; }
}
