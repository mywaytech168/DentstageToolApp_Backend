var builder = WebApplication.CreateBuilder(args);

// ---------- 服務註冊區 ----------
// 註冊控制器，提供 API 與路由的基礎功能
builder.Services.AddControllers();
// 啟用 Swagger 方便初期開發與溝通 API 規格
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------- 中介層設定區 ----------
// 在開發階段顯示 Swagger UI，方便快速驗證 API
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 啟用 HTTPS 重新導向，確保外部存取使用安全通道
app.UseHttpsRedirection();

// 啟用授權流程，後續可延伸加入身份驗證
app.UseAuthorization();

// 將控制器路由對應到實際的端點
app.MapControllers();

// 啟動 Web 應用程式
app.Run();
