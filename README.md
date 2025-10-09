# DentstageToolApp 後端專案

本儲存庫提供「卓越凹痕工廠 APP」後端服務的基礎架構。本次提交完成 .NET 8 Web API 專案骨架，未來可逐步擴充各式業務模組。

## 專案結構

```
DentstageToolApp_Backend/
├─ DentstageToolApp_Backend.sln        # 方案檔，統一管理所有後端模組
├─ global.json                          # 指定 .NET SDK 版本，確保環境一致
├─ docs/
│  └─ swagger/                          # 匯出的 Swagger 規格文件
│     └─ dentstage-tool-app-api-v1.json # 初始健康檢查 API 文件
├─ src/
│  ├─ DentstageToolApp.Api/             # Web API 專案目錄
│  │  ├─ Controllers/
│  │  │  └─ HealthCheckController.cs    # 健康檢查 API，提供服務狀態
│  │  ├─ docs/                          # API 測試與操作說明文件
│  │  │  ├─ api-flow-tester.html        # API 流程測試工具頁面
│  │  │  ├─ quotation-create-guide.html # 新增估價單流程與欄位對照說明頁面
│  │  │  └─ api/
│  │  │     └─ index.html               # API 說明頁面
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
5. 服務啟動後，可透過 [https://localhost:7249/swagger/index.html](https://localhost:7249/swagger/index.html) 瀏覽 Swagger API 說明文件。
6. 若需離線瀏覽 API，可打開 `docs/swagger/dentstage-tool-app-api-v1.json` 於 Swagger UI 或 Postman 匯入檢視。
7. 需要了解新增估價單欄位與流程，可直接開啟 `src/DentstageToolApp.Api/docs/quotation-create-guide.html` 進行瀏覽。

> 若在本地環境使用 Visual Studio 或 Rider，請直接開啟 `DentstageToolApp_Backend.sln` 方案檔。

## 後續開發提醒

- 所有程式碼請維持中文註解，清楚說明邏輯與目的。
- 新增模組時，建議以資料夾劃分領域 (例如 `Modules/Orders`)，方便維護。
- 若需調整資料表結構，可在 `DentstageToolApp.Infrastructure` 專案中修改 EF Core 實體或 `DentstageToolAppContext` 對應設定。

## 車牌辨識模組使用指引

為了支援車牌辨識流程，專案已改用 Tesseract OCR。部署前請依照以下步驟準備環境與測試：

1. **準備 Tesseract OCR 執行環境**
   - 於伺服器安裝 Tesseract（建議 4.x 以上版本），並確認系統可找到對應的 `tesseract` 程式及 `tessdata` 目錄。
   - 將需使用的語系訓練資料（例如英數混合的 `eng.traineddata`、臺灣車牌常用的 `chi_tra.traineddata`）放置在指定的 `tessdata` 資料夾中。

2. **設定組態檔**
   - 編輯 `src/DentstageToolApp.Api/appsettings.json` 或對應環境檔，調整 `TesseractOcr` 區段：

     ```json
     "TesseractOcr": {
       "TessDataPath": "/usr/share/tesseract-ocr/4.00/tessdata",
       "Language": "eng",
       "CharacterWhitelist": "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
       "PageSegmentationMode": "SingleBlock"
     }
     ```

   - `TessDataPath` 為 tessdata 資料夾的完整路徑；`Language` 可使用 `eng+chi_tra` 形式載入多種語系。
   - 若車牌僅包含英數字，建議設定 `CharacterWhitelist`，可降低誤判率；`PageSegmentationMode` 可依影像拍攝情境調整。

3. **測試 API**
   - 服務啟動後，透過下列範例指令進行驗證：

     ```bash
    curl -X POST "https://localhost:7249/api/car-plates/recognitions" \
       -H "Authorization: Bearer <JWT_TOKEN>" \
       -F "image=@/path/to/license.jpg"
     ```

   - 若需改用 Base64，可改用 `-F "imageBase64=$(cat encoded.txt)"` 方式提交，不需額外提供檔案欄位。
   - 影像辨識取得車牌號後，可進一步呼叫維修紀錄查詢 API：

     ```bash
    curl -X POST "https://localhost:7249/api/car-plates/search" \
       -H "Authorization: Bearer <JWT_TOKEN>" \
       -H "Content-Type: application/json" \
       -d '{
         "licensePlateNumber": "ABC1234"
       }'
     ```

     成功時會回傳車牌正規化結果、車輛資訊與維修紀錄清單；若查無資料則回傳 404 問題詳情。

4. **驗收清單**
   - [ ] 成功回傳車牌號碼、品牌、型號、顏色與維修紀錄旗標。
   - [ ] 車牌搜尋 API 可依照車牌回傳歷史維修紀錄清單。
   - [ ] 錯誤情境（影像模糊、Base64 格式錯誤、組態缺失）能得到中文錯誤訊息。
   - [ ] 已於伺服器安裝 Tesseract 並放置所需語系訓練資料，服務啟動時無錯誤紀錄。

## Swagger 文件使用指引

1. **本機預覽**：啟動 API 專案後造訪 `/swagger`，即可檢視包含控制器摘要與範例的即時文件。
2. **離線分享**：匯出自動產生的文件時，可複製 `docs/swagger/dentstage-tool-app-api-v1.json` 提供給前端或外部廠商。
3. **驗收清單**：
   - [ ] Swagger UI 能正常開啟且顯示 `Dentstage Tool App API v1`。
   - [ ] 健康檢查端點擁有中文摘要與範例回應。
   - [ ] `docs/swagger` 目錄內文件版本與服務端口一致，避免文件落後。

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
