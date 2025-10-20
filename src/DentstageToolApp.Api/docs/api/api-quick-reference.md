# API 操作快速帶入指南

本文件整理目前後端所有公開 API 端點，提供以文件撰寫方式快速建立 Postman、Hoppscotch、VSCode REST Client 等工具的請求範本，無須依賴 Swagger 匯入。建議於專案啟動後以實際 Base URL（例如 `https://{domain}/` 或 `https://localhost:5001/`）搭配下述路徑呼叫。

> **授權提醒**：除 `api/healthcheck` 外，其餘端點皆預設啟用 JWT 驗證。請於請求標頭帶入 `Authorization: Bearer {token}`。

---

## Auth 模組（`api/auth`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | 以裝置機碼登入取得 Access/Refresh Token，並回傳門市資訊。 | Body 採 JSON，對應 `LoginRequest`。 |
| POST | `/api/auth/token/refresh` | 使用 Refresh Token 換發新的權杖。 | Body 採 JSON，對應 `RefreshTokenRequest`。 |
| GET | `/api/auth/info` | 查詢目前登入者資訊。 | 需攜帶權杖。 |

**登入請求範例**
```http
POST /api/auth/login HTTP/1.1
Content-Type: application/json

{
  "deviceKey": "CFC29A95-885C-CF45-A91C-F0DD3F1DDD7C"
}
```
- `deviceKey`：裝置機碼，長度上限 150。 【F:src/DentstageToolApp.Api/Auth/LoginRequest.cs†L5-L15】
- 回應會附帶 `storeId`、`storeType` 與 `serverRole` 欄位，來源取自 `sync_machine_profiles`。

**Refresh Token 請求範例**
```http
POST /api/auth/token/refresh HTTP/1.1
Content-Type: application/json
Authorization: Bearer {舊的AccessToken}

{
  "refreshToken": "{舊的RefreshToken}",
  "deviceKey": "CFC29A95-885C-CF45-A91C-F0DD3F1DDD7C"
}
```
- `refreshToken`：舊 Refresh Token。 【F:src/DentstageToolApp.Api/Auth/RefreshTokenRequest.cs†L5-L21】
- `deviceKey`：再次驗證裝置綁定。

---

## 管理者帳號模組（`api/admin/accounts`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| POST | `/api/admin/accounts` | 建立使用者帳號與裝置機碼。 | Body 採 JSON，對應 `CreateUserDeviceRequest`。 |

**請求欄位重點**
- `displayName`：必填顯示名稱。 【F:src/DentstageToolApp.Api/Admin/CreateUserDeviceRequest.cs†L5-L15】
- `role`：選填角色識別。
- `deviceKey`：必填裝置機碼。 【F:src/DentstageToolApp.Api/Admin/CreateUserDeviceRequest.cs†L23-L28】
- `deviceName`、`operatorName`：裝置說明與建立者備註。 【F:src/DentstageToolApp.Api/Admin/CreateUserDeviceRequest.cs†L30-L40】

---

## 車輛模組（`api/cars`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/cars?page=1&pageSize=20` | 以分頁取得車輛列表。 | Query 對應 `PaginationRequest`，預設每頁 20 筆。 |
| POST | `/api/cars` | 新增車輛。 | Body 採 JSON，對應 `CreateCarRequest`。 |
| POST | `/api/cars/edit` | 編輯車輛。 | Body 採 JSON，對應 `EditCarRequest`。 |

**新增車輛主要欄位**
- `carPlateNumber`：必填車牌號碼。 【F:src/DentstageToolApp.Api/Cars/CreateCarRequest.cs†L10-L15】
- `brandUid`、`modelUid`：對應品牌/車型 UID。 【F:src/DentstageToolApp.Api/Cars/CreateCarRequest.cs†L17-L27】
- `color`、`remark`：車色與備註。 【F:src/DentstageToolApp.Api/Cars/CreateCarRequest.cs†L29-L39】
- `mileage`：最新里程數（公里），若現場量測可一併帶入。 【F:src/DentstageToolApp.Api/Models/Cars/CreateCarRequest.cs†L41-L45】

**編輯車輛需加帶欄位**
- `carUid`：欲更新的車輛識別碼。 【F:src/DentstageToolApp.Api/Cars/EditCarRequest.cs†L10-L15】
- 其餘欄位與新增格式相同，可選擇保留或清空。 【F:src/DentstageToolApp.Api/Cars/EditCarRequest.cs†L17-L46】
- 若需更新里程數請帶入 `mileage`，後端會同步覆寫車輛與估價單的里程欄位。 【F:src/DentstageToolApp.Api/Services/Car/CarManagementService.cs†L180-L221】

---

## 客戶模組（`api/customers`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/customers?page=1&pageSize=20` | 以分頁取得客戶列表。 | Query 對應 `PaginationRequest`，回傳 `PaginationMetadata`。 |
| POST | `/api/customers` | 新增客戶資料。 | Body 採 JSON，對應 `CreateCustomerRequest`。 |
| POST | `/api/customers/edit` | 編輯客戶資料。 | Body 採 JSON，對應 `EditCustomerRequest`。 |
| POST | `/api/customers/phone-search` | 依電話搜尋客戶與維修統計。 | Body 採 JSON，對應 `CustomerPhoneSearchRequest`。 |

## 品牌模組（`api/brands`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/brands?page=1&pageSize=20` | 以分頁取得品牌列表。 | Query 對應 `PaginationRequest`，依名稱排序。 |
| POST | `/api/brands` | 新增品牌。 | Body 採 JSON，對應 `CreateBrandRequest`。 |
| POST | `/api/brands/edit` | 編輯品牌。 | Body 採 JSON，對應 `EditBrandRequest`。 |
| POST | `/api/brands/delete` | 刪除品牌。 | Body 採 JSON，對應 `DeleteBrandRequest`。 |

> 品牌列表回應會提供 `pagination` 欄位，包含 `page`、`pageSize`、`totalCount` 與 `totalPages`，可直接驅動前端分頁元件。 【F:src/DentstageToolApp.Api/Models/Brands/BrandListResponse.cs†L8-L24】

## 服務類別模組（`api/service-categories`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/service-categories?page=1&pageSize=20` | 以分頁取得服務類別。 | Query 對應 `PaginationRequest`。 |
| POST | `/api/service-categories` | 新增服務類別。 | Body 採 JSON，對應 `CreateServiceCategoryRequest`。 |
| POST | `/api/service-categories/edit` | 編輯服務類別。 | Body 採 JSON，對應 `EditServiceCategoryRequest`。 |
| POST | `/api/service-categories/delete` | 刪除服務類別。 | Body 採 JSON，對應 `DeleteServiceCategoryRequest`。 |

> 回應的 `pagination` 欄位與品牌模組一致，方便統一處理分頁邏輯。 【F:src/DentstageToolApp.Api/Models/ServiceCategories/ServiceCategoryListResponse.cs†L8-L24】

## 門市模組（`api/stores`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/stores?page=1&pageSize=20` | 以分頁取得門市列表。 | Query 對應 `PaginationRequest`。 |
| POST | `/api/stores` | 新增門市。 | Body 採 JSON，對應 `CreateStoreRequest`。 |
| POST | `/api/stores/edit` | 編輯門市。 | Body 採 JSON，對應 `EditStoreRequest`。 |
| POST | `/api/stores/delete` | 刪除門市。 | Body 採 JSON，對應 `DeleteStoreRequest`。 |

> 門市列表同樣回傳 `pagination`，可搭配技師查詢功能使用。 【F:src/DentstageToolApp.Api/Models/Stores/StoreListResponse.cs†L8-L24】

**新增／編輯欄位重點**
- `customerName`：必填客戶名稱。 【F:src/DentstageToolApp.Api/Customers/CreateCustomerRequest.cs†L10-L15】【F:src/DentstageToolApp.Api/Customers/EditCustomerRequest.cs†L17-L22】
- `phone`、`email`：聯絡資訊（會自動整理格式）。 【F:src/DentstageToolApp.Api/Customers/CreateCustomerRequest.cs†L17-L52】
- `category`、`gender`、`county`、`township`、`source`、`reason`、`remark`：分類與補充說明。 【F:src/DentstageToolApp.Api/Customers/CreateCustomerRequest.cs†L23-L70】
- 編輯時需額外帶入 `customerUid`。 【F:src/DentstageToolApp.Api/Customers/EditCustomerRequest.cs†L10-L15】

**電話搜尋請求**
```http
POST /api/customers/phone-search HTTP/1.1
Content-Type: application/json

{
  "phone": "0988123456"
}
```
- `phone`：必填且長度上限 50。 【F:src/DentstageToolApp.Api/Customers/CustomerPhoneSearchRequest.cs†L5-L15】

---

## 估價單模組（`api/quotations`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/quotations` | 以查詢參數取得估價單列表。 | Query 對應 `QuotationListQuery`。 |
| POST | `/api/quotations` | 以 Body 送出查詢條件取得列表。 | JSON 對應 `QuotationListQuery`。 |
| POST | `/api/quotations/create` | 建立估價單。 | JSON 對應 `CreateQuotationRequest`。 |
| POST | `/api/quotations/detail` | 取得估價單詳細。 | JSON 對應 `GetQuotationRequest`。 |
| POST | `/api/quotations/edit` | 編輯估價單。 | JSON 對應 `UpdateQuotationRequest`。 |
| POST | `/api/quotations/evaluate` | 將狀態更新為估價完成。 | JSON 對應 `QuotationEvaluateRequest`。 |
| POST | `/api/quotations/cancel` | 取消估價或預約。 | JSON 對應 `QuotationCancelRequest`。 |
| POST | `/api/quotations/reserve` | 轉為預約並設定日期。 | JSON 對應 `QuotationReservationRequest`。 |
| POST | `/api/quotations/reserve/update` | 更新既有預約日期。 | JSON 同 `QuotationReservationRequest`。 |
| POST | `/api/quotations/reserve/cancel` | 取消預約並清除日期。 | JSON 同 `QuotationCancelRequest`。 |
| POST | `/api/quotations/revert` | 將估價單狀態回溯。 | JSON 對應 `QuotationRevertStatusRequest`。 |
| POST | `/api/quotations/maintenance` | 估價單轉維修並產生維修單。 | JSON 對應 `QuotationMaintenanceRequest`。 |

> 估價單詳情提供 `amounts` 物件，包含估價金額、折扣與應付金額欄位，維修單詳情亦沿用相同結構。 【F:src/DentstageToolApp.Api/Quotations/QuotationDetailResponse.cs†L9-L96】【F:src/DentstageToolApp.Api/Services/Quotation/QuotationService.cs†L720-L748】

**列表查詢常用欄位**
- `fixType`：維修類型篩選條件，請使用 dent、beauty、paint、other 等固定鍵值或舊版中文名稱。 【F:src/DentstageToolApp.Api/Quotations/QuotationListQuery.cs†L11-L18】
- `status`：估價單狀態碼（110/180/190/191/195）。 【F:src/DentstageToolApp.Api/Quotations/QuotationListQuery.cs†L16-L19】
- `startDate`、`endDate`、`customerKeyword`、`carPlateKeyword`、`page`、`pageSize`。 【F:src/DentstageToolApp.Api/Quotations/QuotationListQuery.cs†L21-L51】

**建立／編輯估價單主要結構範例**
```json
{
  "quotationNo": "Q25100001",
  "store": {
    "technicianUid": "U_054C053D-FBA6-D843-9BDA-8C68E5027895",
    "source": "官方網站",
    "bookMethod": "LINE 預約",
    "reservationDate": "2024-10-15T10:00:00",
    "repairDate": "2024-10-25T09:00:00"
  },
  "car": {
    "carUid": "Ca_00D20FB3-E0D1-440A-93C4-4F62AB511C2D",
    "mileage": 18500
  },
  "customer": {
    "customerUid": "Cu_1B65002E-EEC5-42FA-BBBB-6F5E4708610A"
  },
  "damages": {
    "dent": [
      {
        "photos": "Ph_759F19C7-5D62-4DB2-8021-2371C3136F7B",
        "position": "前保桿",
        "dentStatus": "大面積",
        "description": "需板金搭配烤漆",
        "estimatedAmount": 4500,
        "fixType": "凹痕"
      }
    ],
    "paint": [
      {
        "photos": "Ph_1F8AC157-5AC2-4E9C-9E0C-A5E8B4C8F3B0",
        "position": "右後葉子板",
        "dentStatus": "烤漆",
        "description": "刮傷需補土烤漆",
        "estimatedAmount": 3200,
        "fixType": "板烤/鈑烤"
      }
    ]
  },
  "carBodyConfirmation": {
    "signaturePhotoUid": "Ph_D4FB9159-CD9E-473A-A3D9-0A8FDD0B76F8",
    "damageMarkers": [
      {
        "x": 0.42,
        "y": 0.63,
        "hasDent": true,
        "hasScratch": false,
        "hasPaintPeel": false,
        "remark": "主要凹痕"
      }
    ]
  },
  "maintenance": {
    "fixType": "凹痕",
    "reserveCar": true,
    "applyCoating": false,
    "applyWrapping": false,
    "hasRepainted": false,
    "needToolEvaluation": true,
    "otherFee": 800,
    "roundingDiscount": 200,
    "percentageDiscount": 10,
    "discountReason": "回饋老客戶",
    "estimatedRepairDays": 1,
    "estimatedRepairHours": 6,
    "estimatedRestorationPercentage": 90,
    "suggestedPaintReason": null,
    "unrepairableReason": null,
    "remark": "請於修復後通知客戶取車"
  }
}
```
- `store`：需帶技師 UID、來源、預約方式與可選的預約／維修日期。 【F:src/DentstageToolApp.Api/Models/Quotations/CreateQuotationRequest.cs†L52-L81】
- `car`：可額外附上 `mileage` 更新車輛里程。 【F:src/DentstageToolApp.Api/Models/Quotations/CreateQuotationRequest.cs†L362-L384】
- `customer`：沿用客戶主檔資訊，建立或編輯估價單時僅需帶入 `customerUid`。
- `maintenance`：含維修類型、留車、折扣、估工等設定。 【F:src/DentstageToolApp.Api/Quotations/CreateQuotationRequest.cs†L34-L120】
- `damages`：以物件包裝凹痕（dent）、美容（beauty）、鈑烤（paint）、其他（other）四種陣列，每筆資料僅需填入中文 `fixType` 名稱。
- 編輯時需額外帶入 `quotationNo`，其餘欄位結構相同。 【F:src/DentstageToolApp.Api/Quotations/UpdateQuotationRequest.cs†L9-L47】

**狀態操作共通欄位**
- `quotationNo`：所有狀態操作必帶欄位。 【F:src/DentstageToolApp.Api/Quotations/QuotationActionRequestBase.cs†L5-L27】
- `reservationDate`：轉預約／更新預約時必填。 【F:src/DentstageToolApp.Api/Quotations/QuotationReservationRequest.cs†L9-L15】
- `reason`、`clearReservation`：取消預約時可提供原因與是否清除日期。 【F:src/DentstageToolApp.Api/Quotations/QuotationCancelRequest.cs†L5-L19】
- 估價單回溯：狀態依序 195→191→190→180→110 逐步回退，最低回到 110。 【F:src/DentstageToolApp.Api/Services/Quotation/QuotationService.cs†L1913-L1960】

---

## 維修單模組（`api/maintenance-orders`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/maintenance-orders` | 以查詢參數取得維修單列表。 | Query 對應 `MaintenanceOrderListQuery`。 |
| POST | `/api/maintenance-orders` | 透過 Body 查詢維修單列表。 | JSON 同 `MaintenanceOrderListQuery`。 |
| POST | `/api/maintenance-orders/detail` | 取得維修單詳細。 | JSON 對應 `MaintenanceOrderDetailRequest`。 |
| POST | `/api/maintenance-orders/revert` | 維修單狀態回溯。 | JSON 對應 `MaintenanceOrderRevertRequest`。 |
| POST | `/api/maintenance-orders/confirm` | 確認維修開始。 | JSON 對應 `MaintenanceOrderConfirmRequest`。 |
| POST | `/api/maintenance-orders/edit` | 編輯維修單。 | JSON 對應 `UpdateMaintenanceOrderRequest`，欄位與估價單編輯共用。 |
| POST | `/api/maintenance-orders/continue` | 續修維修單。 | JSON 對應 `MaintenanceOrderContinueRequest`。 |
| POST | `/api/maintenance-orders/complete` | 維修完成。 | JSON 對應 `MaintenanceOrderCompleteRequest`。 |
| POST | `/api/maintenance-orders/terminate` | 終止維修。 | JSON 對應 `MaintenanceOrderTerminateRequest`。 |

> 維修單詳情回應沿用 `QuotationDetailResponse` 欄位，並額外提供維修單編號、金額資訊與狀態歷程。 【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderDetailResponse.cs†L9-L42】

**查詢欄位重點**
- `fixType`、`status`、`startDate`、`endDate`：對應篩選條件。 【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderListQuery.cs†L11-L29】
- `page`、`pageSize`：分頁設定。 【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderListQuery.cs†L31-L41】

**單筆操作欄位**
- `orderNo`：維修單編號，為詳細／回溯／確認／編輯／續修／完成／終止的必填欄位。 【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderDetailRequest.cs†L5-L15】【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderRevertRequest.cs†L5-L14】【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderConfirmRequest.cs†L5-L14】【F:src/DentstageToolApp.Api/MaintenanceOrders/UpdateMaintenanceOrderRequest.cs†L10-L18】【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderContinueRequest.cs†L10-L14】【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderCompleteRequest.cs†L10-L14】【F:src/DentstageToolApp.Api/MaintenanceOrders/MaintenanceOrderTerminateRequest.cs†L10-L14】
- 維修單回溯：狀態依序 295→290→220→210 逐步回退，最低回到 210。 【F:src/DentstageToolApp.Api/Services/MaintenanceOrder/MaintenanceOrderService.cs†L187-L244】【F:src/DentstageToolApp.Api/Services/MaintenanceOrder/MaintenanceOrderService.cs†L952-L1038】
- 續修維修單會先將原工單標記為 295，並複製估價單與圖片供後續續修作業。 【F:src/DentstageToolApp.Api/Services/MaintenanceOrder/MaintenanceOrderService.cs†L458-L546】【F:src/DentstageToolApp.Api/Services/Quotation/QuotationService.cs†L1275-L1337】
- `quotationNo`：編輯維修單時可帶入以驗證估價單關聯，沿用估價單編輯欄位。 【F:src/DentstageToolApp.Api/MaintenanceOrders/UpdateMaintenanceOrderRequest.cs†L10-L18】【F:src/DentstageToolApp.Api/Quotations/UpdateQuotationRequest.cs†L9-L47】
- `store`：改派技師或更新預約／維修日期時使用，欄位與估價單編輯共用（`technicianUid`、`source`、`reservationDate`、`repairDate`）。 【F:src/DentstageToolApp.Api/Quotations/UpdateQuotationRequest.cs†L13-L47】【F:src/DentstageToolApp.Api/Services/Quotation/QuotationService.cs†L823-L1013】

---

## 圖片模組（`api/photos`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| POST | `/api/photos` | 上傳估價／維修照片並取得 `photoUid`。 | `multipart/form-data`，檔案欄位為 `file`。 |
| GET | `/api/photos/{photoUid}` | 下載指定圖片。 | 需帶有效的 `photoUid`。 |

**上傳注意事項**
- 表單欄位名稱為 `file`，大小限制 50 MB。 【F:src/DentstageToolApp.Api/Controllers/PhotosController.cs†L37-L69】
- 成功時回傳 `photoUid` 與檔案資訊，可直接寫入估價單。 【F:src/DentstageToolApp.Api/Controllers/PhotosController.cs†L70-L101】

---

## 車牌模組（`api/car-plates`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| POST | `/api/car-plates/recognitions` | 上傳車牌影像進行辨識。 | `multipart/form-data`，支援檔案或 Base64 欄位。 |
| POST | `/api/car-plates/search` | 依車牌號碼查詢歷史維修資料。 | JSON 對應 `CarPlateMaintenanceHistoryRequest`。 |

**辨識請求欄位**
- `image`：上傳檔案。 【F:src/DentstageToolApp.Api/CarPlates/CarPlateRecognitionRequest.cs†L10-L13】
- `imageBase64`：若無檔案可帶 Base64 字串。 【F:src/DentstageToolApp.Api/CarPlates/CarPlateRecognitionRequest.cs†L15-L18】

**維修紀錄查詢欄位**
- `carPlateNumber`：欲查詢的車牌號碼。 【F:src/DentstageToolApp.Api/CarPlates/CarPlateMaintenanceHistoryRequest.cs†L3-L11】

---

## 品牌／型號模組（`api/brands-models`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/brands-models` | 取得品牌與車型清單。 | 無需額外參數。 |

此端點會回傳品牌與車型的樹狀資料，供前端建立下拉清單。 【F:src/DentstageToolApp.Api/Controllers/BrandModelsController.cs†L16-L64】

---

## 技師模組（`api/technicians`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/technicians?page=1&pageSize=20` | 依登入者所屬門市取得技師名單。 | 需帶權杖，Query 對應 `PaginationRequest`。 |
| GET | `/api/technicians/{storeUid}?page=1&pageSize=20` | 指定門市分頁查詢技師名單。 | 適合總部查詢其他門市人員。 |

查詢時後端會從 JWT 取出 `sub` 或 `displayName` 等欄位，並回傳對應技師資訊列表。 【F:src/DentstageToolApp.Api/Controllers/TechniciansController.cs†L17-L134】

- 回傳欄位包含 `jobTitle` 與 `pagination`，可直接顯示技師職稱與分頁資訊。 【F:src/DentstageToolApp.Api/Models/Technicians/TechnicianListResponse.cs†L8-L35】

---

## 健康檢查（`api/healthcheck`）

| 方法 | 路徑 | 功能摘要 | 備註 |
| --- | --- | --- | --- |
| GET | `/api/healthcheck` | 回傳服務狀態與時間戳記。 | 無需授權，可做為監控用途。 |

回應內容範例：
```json
{
  "status": "healthy",
  "message": "後端服務啟動成功",
  "timestamp": "2024-01-01T00:00:00Z"
}
```
【F:src/DentstageToolApp.Api/Controllers/HealthCheckController.cs†L7-L26】

---

## 使用建議
1. 依需求建立環境變數管理 Base URL 與權杖，避免重複輸入。
2. 測試檔案上傳時可使用 Postman 的 form-data 或 VSCode REST Client 的 `@` 語法指向本地檔案。
3. 若需批次測試狀態流轉，建議按照「建立估價單 → 轉預約／轉維修 → 建立維修單 → 確認維修」順序執行，方便追蹤流程。
4. 所有狀態變更皆需權杖內含 `displayName` 或 `sub`，請確認登入流程正常取得 Claims。
