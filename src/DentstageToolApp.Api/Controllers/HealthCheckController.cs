using Microsoft.AspNetCore.Mvc;

namespace DentstageToolApp.Api.Controllers;

/// <summary>
/// 健康檢查控制器，用於確認服務是否正常運作。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    /// <summary>
    /// 回傳系統狀態與時間戳記，方便監控與排錯。
    /// </summary>
    /// <returns>含狀態、訊息與時間的健康檢查資訊。</returns>
    [HttpGet]
    public IActionResult Get()
    {
        // 使用匿名物件整理健康檢查資訊，後續可擴充更多系統資料
        var response = new
        {
            status = "healthy",
            message = "後端服務啟動成功",
            timestamp = DateTimeOffset.UtcNow
        };

        return Ok(response);
    }
}
