using System.IO;
using System.Reflection;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- 服務註冊區 ----------
// 註冊控制器，提供 API 與路由的基礎功能
builder.Services.AddControllers();
// 啟用 Swagger 方便初期開發與溝通 API 規格
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // 建立 API 說明文件的基本資訊，方便協作與客戶對齊規格
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Dentstage Tool App API",
        Version = "v1",
        Description = "提供凹痕工廠後台系統所需的後端服務介面與健康檢查端點。",
        Contact = new OpenApiContact
        {
            Name = "Dentstage Tool App Team",
            Email = "support@dentstage.com"
        }
    });

    // 讀取 XML 註解檔案，讓 Swagger UI 可以呈現中文摘要說明
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});
// 設定資料庫內容類別，利用 DB First 模型對應實際資料表結構
builder.Services.AddDbContext<DentstageToolAppContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DentstageToolAppDatabase")));

var app = builder.Build();

// ---------- 中介層設定區 ----------
// 在開發階段顯示 Swagger UI，方便快速驗證 API
if (app.Environment.IsDevelopment())
{
    // 以具名文件來源呈現 Swagger UI，並提供友善的 API 首頁描述
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dentstage Tool App API v1");
        options.DocumentTitle = "Dentstage Tool App 後端 API 文件";
        options.RoutePrefix = "swagger";
    });
}

// 啟用 HTTPS 重新導向，確保外部存取使用安全通道
app.UseHttpsRedirection();

// 啟用授權流程，後續可延伸加入身份驗證
app.UseAuthorization();

// 將控制器路由對應到實際的端點
app.MapControllers();

// 啟動 Web 應用程式
app.Run();
