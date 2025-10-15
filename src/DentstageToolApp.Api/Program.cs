using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DentstageToolApp.Api.BackgroundJobs;
using DentstageToolApp.Api.Models.Options;
using DentstageToolApp.Api.Models.Sync;
using DentstageToolApp.Api.Infrastructure.Database;
using DentstageToolApp.Api.Infrastructure.System;
using DentstageToolApp.Api.Services.Admin;
using DentstageToolApp.Api.Services.Auth;
using DentstageToolApp.Api.Services.BrandModels;
using DentstageToolApp.Api.Services.Brand;
using DentstageToolApp.Api.Services.Car;
using DentstageToolApp.Api.Services.CarPlate;
using DentstageToolApp.Api.Services.Model;
using DentstageToolApp.Api.Services.Quotation;
using DentstageToolApp.Api.Services.Photo;
using DentstageToolApp.Api.Services.MaintenanceOrder;
using DentstageToolApp.Api.Services.Technician;
using DentstageToolApp.Api.Services.Customer;
using DentstageToolApp.Api.Services.ServiceCategory;
using DentstageToolApp.Api.Services.Store;
using DentstageToolApp.Api.Services.Sync;
using DentstageToolApp.Api.Swagger;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------- 服務註冊區 ----------
// 註冊控制器，提供 API 與路由的基礎功能
builder.Services.AddControllers();
// 先讀取 Swagger 相關組態，包含自訂文件連結的基底路徑
var swaggerSection = builder.Configuration.GetSection("Swagger");
var swaggerDocsBaseUrl = swaggerSection.GetValue<string?>("DocsBaseUrl") ?? "/docs/api";
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
        Description = "請直接輸入 JWT，實作需加上Bearer ",
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

    // 注入 Mock 範例過濾器，將屬性定義的 JSON 直接呈現在 Swagger UI 中
    options.OperationFilter<MockRequestExampleOperationFilter>();
    // 自動掛載外部 DOCS 連結，讓開發者可直接跳轉至詳盡教學
    options.OperationFilter<SwaggerExternalDocumentOperationFilter>(swaggerDocsBaseUrl);
});
// 讀取 Swagger 組態，提供後續中介層調整依據
var swaggerEnabled = true;
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
builder.Services.Configure<TesseractOcrOptions>(builder.Configuration.GetSection("TesseractOcr"));
builder.Services.Configure<PhotoStorageOptions>(builder.Configuration.GetSection("PhotoStorage"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
if (jwtOptions is null || string.IsNullOrWhiteSpace(jwtOptions.Secret))
{
    throw new InvalidOperationException("未設定 Jwt.Secret，無法產生簽章金鑰。");
}

var tesseractOptions = builder.Configuration.GetSection("TesseractOcr").Get<TesseractOcrOptions>();
if (tesseractOptions is null || string.IsNullOrWhiteSpace(tesseractOptions.TessDataPath))
{
    throw new InvalidOperationException("未設定 TesseractOcr.TessDataPath，無法啟動車牌辨識服務。");
}

var syncOptionsSection = builder.Configuration.GetSection("Sync");
var syncOptions = new SyncOptions();
syncOptionsSection.Bind(syncOptions);
syncOptions.Transport = SyncTransportModes.Normalize(syncOptions.Transport);

// ---------- 解析本機同步機碼 ----------
// 依序由環境變數、外部檔案與硬體指紋推導同步機碼，避免每台機器都得手動調整設定
syncOptions.MachineKey = LocalMachineKeyResolver.ResolveMachineKey(syncOptions.MachineKey, builder.Environment.ContentRootPath);
if (string.IsNullOrWhiteSpace(syncOptions.MachineKey))
{
    throw new InvalidOperationException("無法由本機環境推導同步機碼，請確認環境變數、機碼檔案或硬體資訊設定。");
}

if (!string.IsNullOrWhiteSpace(syncOptions.MachineKey))
{
    // ---------- 透過同步機碼向資料庫查詢實際角色與門市設定 ----------
    using var tempProvider = builder.Services.BuildServiceProvider();
    await using var scope = tempProvider.CreateAsyncScope();
    var syncDbContext = scope.ServiceProvider.GetRequiredService<DentstageToolAppContext>();
    // ---------- 以裝置註冊綁定同步設定，避免額外維護機碼對應表 ----------
    var deviceRegistration = await syncDbContext.DeviceRegistrations
        .AsNoTracking()
        .Include(registration => registration.UserAccount)
        .FirstOrDefaultAsync(registration => registration.DeviceKey == syncOptions.MachineKey);

    if (deviceRegistration is null)
    {
        throw new InvalidOperationException($"找不到同步機碼 {syncOptions.MachineKey} 對應的裝置註冊資料，請確認 DeviceRegistrations 是否已建立該筆機碼。");
    }

    if (deviceRegistration.UserAccount is null)
    {
        throw new InvalidOperationException("裝置註冊缺少對應的使用者帳號，無法推導伺服器角色，請確認 DeviceRegistrations.UserUID 設定是否正確。");
    }

    if (string.IsNullOrWhiteSpace(deviceRegistration.UserAccount.ServerRole))
    {
        throw new InvalidOperationException("使用者帳號尚未設定 ServerRole 欄位，無法判斷中央或門市角色，請至 UserAccounts 補齊資料。");
    }

    // 使用者 UID 即為門市識別碼，角色欄位對應門市型態，統一由 SyncOptions 儲存供後續背景任務使用
    syncOptions.ApplyMachineProfile(deviceRegistration.UserAccount.ServerRole, deviceRegistration.UserAccount.UserUid, deviceRegistration.UserAccount.Role);

    // ---------- 查詢中央伺服器帳號，取得對外同步 IP ----------
    var centralCandidates = await syncDbContext.UserAccounts
        .AsNoTracking()
        .Where(account => !string.IsNullOrWhiteSpace(account.ServerRole))
        .ToListAsync();

    var centralAccounts = centralCandidates
        .Where(account => string.Equals(SyncServerRoles.Normalize(account.ServerRole), SyncServerRoles.CentralServer, StringComparison.Ordinal))
        .ToList();

    if (centralAccounts.Count == 0)
    {
        throw new InvalidOperationException("找不到 ServerRole = Central 的中央伺服器帳號，請於 UserAccounts 建立中央環境設定。");
    }

    if (centralAccounts.Count > 1)
    {
        throw new InvalidOperationException("偵測到多筆 ServerRole = Central 的帳號，請確認僅保留單一中央伺服器設定避免同步混亂。");
    }

    var centralAccount = centralAccounts[0];
    if (string.IsNullOrWhiteSpace(centralAccount.ServerIp))
    {
        throw new InvalidOperationException("中央伺服器帳號缺少 ServerIp 欄位設定，請於 UserAccounts.ServerIp 填寫中央對外 IP。");
    }

    // ---------- 將中央伺服器 IP 存入同步選項，後續背景任務可用來建立中央連線 ----------
    syncOptions.CentralServerIp = centralAccount.ServerIp;
}

var normalizedRole = syncOptions.NormalizedServerRole;
if (string.IsNullOrWhiteSpace(normalizedRole))
{
    throw new InvalidOperationException("請設定 Sync.MachineKey 或 Sync.ServerRole，以便辨識中央或門市角色。");
}

if (!syncOptions.HasResolvedMachineProfile)
{
    throw new InvalidOperationException("同步機碼尚未補齊門市資訊，請檢查 UserAccounts.Role 是否已設定門市型態。");
}

if (syncOptions.IsStoreRole && string.Equals(syncOptions.Transport, SyncTransportModes.Http, StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(syncOptions.CentralApiBaseUrl))
    {
        throw new InvalidOperationException("門市環境需設定 Sync.CentralApiBaseUrl，才能呼叫中央同步 API。");
    }
}

builder.Services.AddSingleton(syncOptions);
builder.Services.AddSingleton<IOptions<SyncOptions>>(Options.Create(syncOptions));

if (syncOptions.UseMessageQueue)
{
    var queueOptions = syncOptions.Queue ?? new SyncQueueOptions();
    if (string.IsNullOrWhiteSpace(queueOptions.HostName))
    {
        throw new InvalidOperationException("Sync.Transport 設為 RabbitMq 時，必須設定 Sync.Queue.HostName。");
    }

    if (string.IsNullOrWhiteSpace(queueOptions.RequestQueue) || string.IsNullOrWhiteSpace(queueOptions.ResponseQueue))
    {
        throw new InvalidOperationException("Sync.Transport 設為 RabbitMq 時，需設定 Sync.Queue.RequestQueue 與 Sync.Queue.ResponseQueue。");
    }
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
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IMaintenanceOrderService, MaintenanceOrderService>();
builder.Services.AddScoped<ICarPlateRecognitionService, CarPlateRecognitionService>();
builder.Services.AddScoped<ICarManagementService, CarManagementService>();
builder.Services.AddScoped<ICarQueryService, CarQueryService>();
builder.Services.AddScoped<IBrandManagementService, BrandManagementService>();
builder.Services.AddScoped<IBrandQueryService, BrandQueryService>();
builder.Services.AddScoped<IModelManagementService, ModelManagementService>();
builder.Services.AddScoped<IBrandModelQueryService, BrandModelQueryService>();
builder.Services.AddScoped<ICustomerManagementService, CustomerManagementService>();
builder.Services.AddScoped<ICustomerLookupService, CustomerLookupService>();
builder.Services.AddScoped<IServiceCategoryManagementService, ServiceCategoryManagementService>();
builder.Services.AddScoped<IServiceCategoryQueryService, ServiceCategoryQueryService>();
builder.Services.AddScoped<IStoreManagementService, StoreManagementService>();
builder.Services.AddScoped<IStoreQueryService, StoreQueryService>();
builder.Services.AddScoped<ITechnicianQueryService, TechnicianQueryService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();
builder.Services.AddHttpClient<IRemoteSyncApiClient, RemoteSyncApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(syncOptions.CentralApiBaseUrl))
    {
        client.BaseAddress = new Uri(syncOptions.CentralApiBaseUrl, UriKind.Absolute);
    }
});
builder.Services.AddHostedService<RefreshTokenCleanupService>();
if (syncOptions.IsStoreRole)
{
    // ---------- 直營或連盟門市背景同步排程 ----------
    builder.Services.AddHostedService<StoreSyncBackgroundService>();
}

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

// 針對 docs 目錄提供靜態檔案服務，讓估價單流程說明頁面可直接透過瀏覽器檢視
var documentationDirectory = Path.Combine(app.Environment.ContentRootPath, "docs");
if (Directory.Exists(documentationDirectory))
{
    // 使用實體檔案提供者指向 docs 目錄，並以 /docs 作為網址路徑前綴
    var documentationFileProvider = new PhysicalFileProvider(documentationDirectory);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = documentationFileProvider,
        RequestPath = "/docs"
    });
}

// 啟用身份驗證與授權流程，確保保護後續 API
app.UseAuthentication();
app.UseAuthorization();

// 將控制器路由對應到實際的端點
app.MapControllers();

// ---------- 資料庫結構補強 ----------
// 啟動時補齊缺漏欄位，避免舊資料庫缺乏 TechnicianUID 欄位導致查詢失敗。
using (var scope = app.Services.CreateScope())
{
    var schemaInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaInitializer>();
    await schemaInitializer.EnsureQuotationTechnicianColumnAsync();
}

// 啟動 Web 應用程式
await app.RunAsync();
