# DentstageToolApp 後端專案

本儲存庫提供「卓越凹痕工廠 APP」後端服務的基礎架構。本次提交完成 .NET 8 Web API 專案骨架，未來可逐步擴充各式業務模組。

## 專案結構

```
DentstageToolApp_Backend/
├─ DentstageToolApp_Backend.sln        # 方案檔，統一管理所有後端模組
├─ global.json                          # 指定 .NET SDK 版本，確保環境一致
├─ src/
│  └─ DentstageToolApp.Api/             # Web API 專案目錄
│     ├─ Controllers/
│     │  └─ HealthCheckController.cs    # 健康檢查 API，提供服務狀態
│     ├─ Program.cs                     # 服務啟動與中介層設定
│     ├─ appsettings*.json              # 組態檔，可依環境調整
│     └─ Properties/launchSettings.json # 開發階段啟動設定
├─ db_docs/                             # 既有資料庫文件
└─ 卓越-凹痕工廠APP重製-頁面規劃-API表 - 後台.csv
```

## 開發環境建議

1. 安裝 [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. 於專案根目錄執行 `dotnet restore` 下載所需套件。
3. 以 `dotnet run --project src/DentstageToolApp.Api` 啟動後端服務。
4. 服務啟動後，可透過 `https://localhost:7249/swagger` 瀏覽 API 說明文件。

> 若在本地環境使用 Visual Studio 或 Rider，請直接開啟 `DentstageToolApp_Backend.sln` 方案檔。

## 後續開發提醒

- 所有程式碼請維持中文註解，清楚說明邏輯與目的。
- 新增模組時，建議以資料夾劃分領域 (例如 `Modules/Orders`)，方便維護。
- 若需串接資料庫，請優先建立資料夾 `Infrastructure/` 並撰寫對應的 Repository 或 DbContext。

歡迎依據專案需求持續擴充功能與測試。若需更多背景資訊，請參考 `db_docs/` 與原始 API 規格文件。
