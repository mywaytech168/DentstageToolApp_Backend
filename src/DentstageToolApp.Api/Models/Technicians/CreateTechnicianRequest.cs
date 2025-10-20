using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Models.Technicians;

/// <summary>
/// 建立技師資料的請求模型。
/// </summary>
public class CreateTechnicianRequest
{
    /// <summary>
    /// 技師姓名，供顯示與辨識使用。
    /// </summary>
    [Required(ErrorMessage = "請輸入技師姓名。")]
    [MaxLength(100, ErrorMessage = "技師姓名長度不可超過 100 個字元。")]
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// 技師職稱，方便前端顯示角色資訊。
    /// </summary>
    [MaxLength(50, ErrorMessage = "技師職稱長度不可超過 50 個字元。")]
    public string? JobTitle { get; set; }

    /// <summary>
    /// 隸屬門市識別碼，需與門市主檔對應。
    /// </summary>
    [Required(ErrorMessage = "請選擇技師所屬門市。")]
    [MaxLength(100, ErrorMessage = "門市識別碼長度不可超過 100 個字元。")]
    public string StoreUid { get; set; } = string.Empty;
}
