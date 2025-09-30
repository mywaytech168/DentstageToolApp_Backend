# DentstageToolApp 後端專案

本儲存庫提供「卓越凹痕工廠 APP」後端服務的基礎架構。本次提交完成 .NET 8 Web API 專案骨架，未來可逐步擴充各式業務模組。

## 專案結構

```
DentstageToolApp_Backend/
├─ DentstageToolApp_Backend.sln        # 方案檔，統一管理所有後端模組
├─ global.json                          # 指定 .NET SDK 版本，確保環境一致
├─ src/
│  ├─ DentstageToolApp.Api/             # Web API 專案目錄
│  │  ├─ Controllers/
│  │  │  └─ HealthCheckController.cs    # 健康檢查 API，提供服務狀態
│  │  ├─ Program.cs                     # 服務啟動、中介層與 DbContext 註冊
│  │  ├─ appsettings*.json              # 組態檔，可依環境調整與設定連線字串
│  │  └─ Properties/launchSettings.json # 開發階段啟動設定
│  └─ DentstageToolApp.Infrastructure/  # DB First 產生的實體與資料庫內容類別
│     ├─ DentstageToolApp.Infrastructure.csproj
│     ├─ Data/
│     │  └─ DentstageToolAppContext.cs  # EF Core DbContext，對應資料表結構
│     └─ Entities/                      # 各資料表實體類別
├─ db_docs/                             # 既有資料庫文件
└─ 卓越-凹痕工廠APP重製-頁面規劃-API表 - 後台.csv
```

## 開發環境建議

1. 安裝 [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. 於專案根目錄執行 `dotnet restore` 下載所需套件。
3. 依據實際環境調整 `appsettings.json` 或 `appsettings.Development.json` 的 `DentstageToolAppDatabase` 連線字串。
4. 以 `dotnet run --project src/DentstageToolApp.Api` 啟動後端服務。
5. 服務啟動後，可透過 `https://localhost:7249/swagger` 瀏覽 API 說明文件。

> 若在本地環境使用 Visual Studio 或 Rider，請直接開啟 `DentstageToolApp_Backend.sln` 方案檔。

## 後續開發提醒

- 所有程式碼請維持中文註解，清楚說明邏輯與目的。
- 新增模組時，建議以資料夾劃分領域 (例如 `Modules/Orders`)，方便維護。
- 若需調整資料表結構，可在 `DentstageToolApp.Infrastructure` 專案中修改 EF Core 實體或 `DentstageToolAppContext` 對應設定。

歡迎依據專案需求持續擴充功能與測試。若需更多背景資訊，請參考 `db_docs/` 與原始 API 規格文件。

## DB First 指令範例與流程

以下提供匯入既有資料庫結構的標準作業流程，可依實際資料庫調整參數與連線字串。

1. **安裝 dotnet-ef 工具**
   ```bash
   dotnet tool install --global dotnet-ef
   ```
   > 若已安裝過，可改執行 `dotnet tool update --global dotnet-ef` 以確保版本一致。

2. **切換至資料存取專案目錄**
   ```bash
   cd src/DentstageToolApp.Infrastructure
   ```

3. **執行 DbContext 與實體模型反向工程**
   ```bash
   dotnet ef dbcontext scaffold "Server=<資料庫位址>;Database=<資料庫名稱>;User Id=<帳號>;Password=<密碼>;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer \
     --context DentstageToolAppContext \
     --context-dir Data \
     --context-namespace DentstageToolApp.Infrastructure.Data \
     --output-dir Entities \
     --namespace DentstageToolApp.Infrastructure.Entities \
     --use-database-names \
     --no-onconfiguring \
     --force
   ```
   - `--use-database-names`：保留資料表與欄位原始命名。
   - `--no-onconfiguring`：避免覆寫 `DbContext` 內的連線設定，由 `Program.cs` 統一管理。
   - `--force`：覆寫既有檔案，更新資料表變更內容。

4. **驗收清單**
   - [ ] `src/DentstageToolApp.Infrastructure/Data/DentstageToolAppContext.cs` 內資料表對應正確。
   - [ ] `src/DentstageToolApp.Infrastructure/Entities` 內所有實體皆成功產生且含中文註解。
   - [ ] `DentstageToolApp.Api` 專案可成功建置並啟動健康檢查 API。

> 若需額外限制資料表範圍，可加上 `--schema` 或 `--table` 參數進行篩選。
