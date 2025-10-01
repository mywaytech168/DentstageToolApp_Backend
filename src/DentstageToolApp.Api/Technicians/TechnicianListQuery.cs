using System.ComponentModel.DataAnnotations;

namespace DentstageToolApp.Api.Technicians;

/// <summary>
/// 技師名單查詢參數，提供前端傳遞店家識別碼。
/// </summary>
public class TechnicianListQuery
{
    /// <summary>
    /// 店家識別碼，僅接受正整數，代表欲查詢的門市。
    /// </summary>
    [Required(ErrorMessage = "請提供店家識別碼。")]
    [Range(1, int.MaxValue, ErrorMessage = "店家識別碼格式不正確。")]
    public int StoreId { get; set; }
}
