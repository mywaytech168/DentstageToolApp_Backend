using System;
using System.IO;
using System.Reflection;
using System.Text;
using DentstageToolApp.Api.BackgroundJobs;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Api.Services.Admin;
using DentstageToolApp.Api.Services.Auth;
using DentstageToolApp.Api.Services.Quotation;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

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

    // 設定 Bearer 驗證資訊，讓 Swagger UI 可以輸入 JWT 並套用到所有請求
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "請在此輸入 JWT，格式為：Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    // 全域套用 Bearer 授權需求，確保 Swagger 自動帶入 JWT 標頭
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// 讀取 Swagger 組態，提供後續中介層調整依據
var swaggerSection = builder.Configuration.GetSection("Swagger");
var swaggerEnabled = swaggerSection.GetValue<bool?>("Enabled") ?? builder.Environment.IsDevelopment();
var swaggerRoutePrefix = swaggerSection.GetValue<string?>("RoutePrefix") ?? "swagger";
var swaggerEndpointName = swaggerSection.GetValue<string?>("EndpointName") ?? "Dentstage Tool App API v1";
var swaggerDocumentTitle = swaggerSection.GetValue<string?>("DocumentTitle") ?? "Dentstage Tool App 後端 API 文件";

// 設定資料庫內容類別，改用 MySQL 連線並確保連線字串存在
var connectionString = builder.Configuration.GetConnectionString("DentstageToolAppDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("未設定 DentstageToolAppDatabase 連線字串，無法啟動資料庫服務。");
}

// 透過 Pomelo 偵測伺服器版本，並設定重試策略提升穩定度
var serverVersion = ServerVersion.AutoDetect(connectionString);
builder.Services.AddDbContext<DentstageToolAppContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
    {
        // 啟用自動重試，避免瞬斷造成的連線失敗
        mySqlOptions.EnableRetryOnFailure();
    }));

// ---------- JWT 與身份驗證設定 ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
if (jwtOptions is null || string.IsNullOrWhiteSpace(jwtOptions.Secret))
{
    throw new InvalidOperationException("未設定 Jwt.Secret，無法產生簽章金鑰。");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ---------- 自訂服務註冊 ----------
builder.Services.AddScoped<IAccountAdminService, AccountAdminService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();

var app = builder.Build();

// ---------- 中介層設定區 ----------
// 依組態判斷是否顯示 Swagger UI，方便快速驗證 API
if (swaggerEnabled)
{
    // 以具名文件來源呈現 Swagger UI，並提供友善的 API 首頁描述
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", swaggerEndpointName);
        options.DocumentTitle = swaggerDocumentTitle;
        options.RoutePrefix = string.IsNullOrWhiteSpace(swaggerRoutePrefix)
            ? string.Empty
            : swaggerRoutePrefix.Trim('/');
    });
}

// 啟用 HTTPS 重新導向，確保外部存取使用安全通道
app.UseHttpsRedirection();

// 啟用身份驗證與授權流程，確保保護後續 API
app.UseAuthentication();
app.UseAuthorization();

// 將控制器路由對應到實際的端點
app.MapControllers();

// 啟動 Web 應用程式
app.Run();
