using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DentstageToolApp.Api.Quotations;

/// <summary>
/// 估價單的服務分類資料集合，依凹痕、鈑烤與其他三大類別呈現。
/// </summary>
public class QuotationServiceCategoryCollection
{
    /// <summary>
    /// 凹痕服務的綜合資訊、傷痕項目與金額。
    /// </summary>
    public QuotationCategoryBlock? Dent { get; set; }

    /// <summary>
    /// 鈑烤服務的綜合資訊、傷痕項目與金額。
    /// </summary>
    public QuotationCategoryBlock? Paint { get; set; }

    /// <summary>
    /// 其他服務的綜合資訊、傷痕項目與金額。
    /// </summary>
    public QuotationCategoryBlock? Other { get; set; }
}

/// <summary>
/// 每個服務類別的資料區塊，整合整體資訊、傷痕清單與金額摘要。
/// </summary>
public class QuotationCategoryBlock
{
    /// <summary>
    /// 類別整體資訊，例如漆況、是否留車等。
    /// </summary>
    [Required]
    public QuotationCategoryOverallInfo Overall { get; set; } = new();

    /// <summary>
    /// 傷痕細項列表，序列化時會轉換成單一物件（圖片、位置、凹痕狀況、說明、預估金額）以符合前端需求。
    /// 新版格式改由頂層 damages 承載，此欄位改保留給舊資料解析使用。
    /// </summary>
    [JsonConverter(typeof(QuotationDamageCollectionConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<QuotationDamageItem>? Damages { get; set; } = new();

    /// <summary>
    /// 類別金額資訊，包含傷痕小計與折扣。
    /// </summary>
    [Required]
    public QuotationCategoryAmount Amount { get; set; } = new();
}

/// <summary>
/// 類別整體資訊欄位，說明車輛狀況與施工評估。
/// </summary>
public class QuotationCategoryOverallInfo
{
    /// <summary>
    /// 漆況說明，例如是否有鍍膜或烤漆紀錄。
    /// </summary>
    public string? PaintCondition { get; set; }

    /// <summary>
    /// 工具評估描述，可用來紀錄特殊注意事項。
    /// </summary>
    public string? ToolEvaluation { get; set; }

    /// <summary>
    /// 是否需要留車施工。
    /// </summary>
    public bool? NeedStay { get; set; }

    /// <summary>
    /// 類別備註，補充估價技師的說明。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 預估維修時間描述，可自訂格式（例如 3 小時或 2~3 天）。
    /// </summary>
    public string? EstimatedRepairTime { get; set; }

    /// <summary>
    /// 預估修復程度，例如 9 成新或仍留痕跡。
    /// </summary>
    public string? EstimatedRestorationLevel { get; set; }

    /// <summary>
    /// 是否評估可維修，false 代表建議改走其他處理方式。
    /// </summary>
    public bool? IsRepairable { get; set; }
}

/// <summary>
/// 單一傷痕的估價資料，包含照片與金額。
/// </summary>
public class QuotationDamageItem
{
    /// <summary>
    /// 傳統版本僅支援單張照片，保留此欄位以維持相容性。
    /// </summary>
    [Obsolete("請改用 Photos 集合傳遞多張傷痕圖片。")]
    [JsonIgnore]
    public string? Photo { get; private set; }

    /// <summary>
    /// 舊欄位別名，允許仍以 photo 傳值，但輸出時隱藏英文欄位。
    /// </summary>
    [JsonPropertyName("photo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPhoto
    {
        get => null;
        set => SetPrimaryPhoto(value);
    }

    private List<QuotationDamagePhoto> _photos = new();

    /// <summary>
    /// 內部使用的圖片清單，作為舊欄位與新欄位的共用儲存。
    /// </summary>
    [JsonIgnore]
    public List<QuotationDamagePhoto> Photos
    {
        get => _photos;
        set
        {
            _photos = value ?? new List<QuotationDamagePhoto>();
            SyncLegacyPhotoFromPhotos();
        }
    }

    /// <summary>
    /// 新欄位：提供前端使用英文欄位「photos」，並將資料寫入共用清單。
    /// 序列化回傳時會輸出主要圖片的 PhotoUID 字串，以符合新版欄位需求。
    /// </summary>
    [JsonPropertyName("photos")]
    public List<QuotationDamagePhoto> DisplayPhotos
    {
        get => Photos;
        set => Photos = value ?? new List<QuotationDamagePhoto>();
    }

    /// <summary>
    /// 舊資料的中文欄位「圖片」，僅用於解析舊版 JSON，輸出時一律改用英文欄位。
    /// </summary>
    [JsonPropertyName("圖片")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<QuotationDamagePhoto>? LegacyChinesePhotos
    {
        get => null;
        set
        {
            // 若舊版資料帶入中文欄位仍需寫入共用集合，避免遺失照片資訊。
            Photos = value ?? new List<QuotationDamagePhoto>();
        }
    }

    /// <summary>
    /// 內部使用的位置字串，供新舊欄位共用。
    /// </summary>
    [JsonIgnore]
    public string? Position { get; set; }

    /// <summary>
    /// 新欄位：提供英文欄位名稱「position」，方便前端直接對應表格欄位。
    /// </summary>
    [JsonPropertyName("position")]
    public string? DisplayPosition
    {
        get => Position;
        set => Position = value;
    }

    /// <summary>
    /// 舊欄位位置（中文「位置」），仍允許舊版請求傳入，輸出時不再顯示中文欄位。
    /// </summary>
    [JsonPropertyName("位置")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyChinesePosition
    {
        get => null;
        set => Position = value;
    }

    /// <summary>
    /// 內部使用的凹痕狀態描述。
    /// </summary>
    [JsonIgnore]
    public string? DentStatus { get; set; }

    /// <summary>
    /// 新欄位：前端使用英文欄位「dentStatus」，與舊資料共用同一來源。
    /// </summary>
    [JsonPropertyName("dentStatus")]
    public string? DisplayDentStatus
    {
        get => DentStatus;
        set => DentStatus = value;
    }

    /// <summary>
    /// 舊欄位：接受中文欄位「凹痕狀況」，避免歷史資料解析失敗。
    /// </summary>
    [JsonPropertyName("凹痕狀況")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyChineseDentStatus
    {
        get => null;
        set => DentStatus = value;
    }

    /// <summary>
    /// 內部使用的備註內容。
    /// </summary>
    [JsonIgnore]
    public string? Description { get; set; }

    /// <summary>
    /// 新欄位：英文欄位名稱「description」，提供表單直接綁定。
    /// </summary>
    [JsonPropertyName("description")]
    public string? DisplayDescription
    {
        get => Description;
        set => Description = value;
    }

    /// <summary>
    /// 舊欄位：接受中文欄位「說明」，避免舊版呼叫失效。
    /// </summary>
    [JsonPropertyName("說明")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyChineseDescription
    {
        get => null;
        set => Description = value;
    }

    /// <summary>
    /// 內部使用的估價金額。
    /// </summary>
    [JsonIgnore]
    public decimal? EstimatedAmount { get; set; }

    /// <summary>
    /// 新欄位：英文欄位名稱「estimatedAmount」，傳入時同步寫入內部欄位。
    /// </summary>
    [JsonPropertyName("estimatedAmount")]
    public decimal? DisplayEstimatedAmount
    {
        get => EstimatedAmount;
        set => EstimatedAmount = value;
    }

    /// <summary>
    /// 舊欄位：接受中文欄位「預估金額」，確保與舊版資料格式相容。
    /// </summary>
    [JsonPropertyName("預估金額")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LegacyChineseEstimatedAmount
    {
        get => null;
        set => EstimatedAmount = value;
    }

    /// <summary>
    /// 將主要圖片同步到舊欄位，維持舊資料格式相容。
    /// </summary>
    private void SyncLegacyPhotoFromPhotos()
    {
        var primaryPhotoUid = _photos.FirstOrDefault()?.PhotoUid;
        if (string.IsNullOrWhiteSpace(primaryPhotoUid))
        {
            Photo = null;
            return;
        }

        Photo = primaryPhotoUid.Trim();
    }

    /// <summary>
    /// 設定主要圖片，同步更新內部圖片集合與舊欄位。
    /// </summary>
    private void SetPrimaryPhoto(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null)
        {
            Photo = null;
            if (_photos.Count > 0)
            {
                _photos = new List<QuotationDamagePhoto>();
            }

            return;
        }

        if (_photos.Count == 0)
        {
            _photos.Add(new QuotationDamagePhoto { PhotoUid = normalized });
        }
        else
        {
            var primary = _photos[0] ?? new QuotationDamagePhoto();
            primary.PhotoUid = normalized;
            _photos[0] = primary;
        }

        Photo = normalized;
    }
}

/// <summary>
/// 傷痕集合的自訂序列化器，將內部列表轉換成單一物件，並保留與舊版陣列格式的相容性。
/// </summary>
public class QuotationDamageCollectionConverter : JsonConverter<List<QuotationDamageItem>>
{
    /// <summary>
    /// 反序列化時允許同時接受陣列與物件格式，確保舊資料不會壞檔。
    /// </summary>
    public override List<QuotationDamageItem> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new List<QuotationDamageItem>();
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var result = new List<QuotationDamageItem>();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                // 逐筆處理傷痕資料，優先嘗試以新版格式反序列化，失敗時再 fallback。
                var item = ParseDamageItem(element, options);
                if (item is not null)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var item = ParseDamageItem(root, options);

            return item is null
                ? new List<QuotationDamageItem>()
                : new List<QuotationDamageItem> { item };
        }

        throw new JsonException("傷痕資料格式不符，請提供物件或陣列。");
    }

    /// <summary>
    /// 序列化時一律輸出陣列格式，避免前端遇到單筆資料時需要額外處理型別差異。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, List<QuotationDamageItem> value, JsonSerializerOptions options)
    {
        // 依最新需求統一輸出陣列格式，避免前端遇到單筆時需額外判斷物件型別。
        writer.WriteStartArray();

        if (value is { Count: > 0 })
        {
            foreach (var item in value)
            {
                WriteSingleDamage(writer, item, options);
            }
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 將單一傷痕輸出為前端期待的物件格式。
    /// </summary>
    private static void WriteSingleDamage(Utf8JsonWriter writer, QuotationDamageItem? item, JsonSerializerOptions options)
    {
        _ = options; // 目前輸出僅需主要圖片識別碼，因此未使用額外序列化選項。
        var target = item ?? new QuotationDamageItem();

        writer.WriteStartObject();

        writer.WritePropertyName("photos");
        var photos = target.Photos ?? new List<QuotationDamagePhoto>();

        // 對外僅回傳主要圖片的 PhotoUID，確保欄位型別為字串符合前端期待。
        var primaryPhotoUid = photos.FirstOrDefault(static p => !string.IsNullOrWhiteSpace(p?.PhotoUid))?.PhotoUid;
        if (!string.IsNullOrWhiteSpace(primaryPhotoUid))
        {
            writer.WriteStringValue(primaryPhotoUid!.Trim());
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("position");
        WriteNullableString(writer, target.DisplayPosition);

        writer.WritePropertyName("dentStatus");
        WriteNullableString(writer, target.DisplayDentStatus);

        writer.WritePropertyName("description");
        WriteNullableString(writer, target.DisplayDescription);

        writer.WritePropertyName("estimatedAmount");
        if (target.DisplayEstimatedAmount.HasValue)
        {
            writer.WriteNumberValue(target.DisplayEstimatedAmount.Value);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 解析單筆傷痕資料，兼容舊版字串圖片欄位與新版陣列格式。
    /// </summary>
    private static QuotationDamageItem? ParseDamageItem(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            // 先嘗試使用原生序列化流程，若欄位型別符合新版格式會直接成功。
            var item = element.Deserialize<QuotationDamageItem>(options);
            if (item is not null)
            {
                return item;
            }
        }
        catch (JsonException)
        {
            // 舊版資料在「圖片」欄位可能以字串傳遞，導致序列化失敗，此處改以人工映射處理。
        }

        // 以逐欄位讀取方式手動建立資料，確保舊版欄位仍可被解析。
        return new QuotationDamageItem
        {
            DisplayPhotos = ReadPhotoList(element, options),
            DisplayPosition = ReadString(element, "position", "位置"),
            DisplayDentStatus = ReadString(element, "dentStatus", "凹痕狀況"),
            DisplayDescription = ReadString(element, "description", "說明"),
            DisplayEstimatedAmount = ReadDecimal(element, "estimatedAmount", "預估金額")
        };
    }

    /// <summary>
    /// 讀取圖片集合，支援陣列與單一物件寫法。
    /// </summary>
    private static List<QuotationDamagePhoto> ReadPhotoList(JsonElement root, JsonSerializerOptions options)
    {
        if (!TryGetProperty(root, out var element, "photos", "圖片", "photo"))
        {
            return new List<QuotationDamagePhoto>();
        }

        return element.ValueKind switch
        {
            JsonValueKind.Array => DeserializePhotoArray(element, options),
            JsonValueKind.Object => new List<QuotationDamagePhoto>
            {
                element.Deserialize<QuotationDamagePhoto>(options) ?? new QuotationDamagePhoto()
            },
            JsonValueKind.String => CreatePhotoListFromString(element.GetString()),
            _ => new List<QuotationDamagePhoto>()
        };
    }

    /// <summary>
    /// 嘗試從物件中讀取字串欄位，並允許同時匹配多組欄位名稱。
    /// </summary>
    private static string? ReadString(JsonElement root, string primaryName, params string[] alternateNames)
    {
        if (!TryGetProperty(root, out var element, primaryName, alternateNames))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    /// <summary>
    /// 嘗試將指定欄位轉換成 decimal，支援字串與數字輸入。
    /// </summary>
    private static decimal? ReadDecimal(JsonElement root, string primaryName, params string[] alternateNames)
    {
        if (!TryGetProperty(root, out var element, primaryName, alternateNames))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// 從 JsonElement 中依序嘗試取得欄位。
    /// </summary>
    private static bool TryGetProperty(JsonElement root, out JsonElement element, string primaryName, params string[] alternateNames)
    {
        if (root.TryGetProperty(primaryName, out element))
        {
            return true;
        }

        foreach (var name in alternateNames)
        {
            if (!string.IsNullOrWhiteSpace(name) && root.TryGetProperty(name, out element))
            {
                return true;
            }
        }

        element = default;
        return false;
    }

    /// <summary>
    /// 將圖片陣列轉換為物件列表。
    /// </summary>
    private static List<QuotationDamagePhoto> DeserializePhotoArray(JsonElement element, JsonSerializerOptions options)
    {
        var photos = new List<QuotationDamagePhoto>();

        foreach (var photoElement in element.EnumerateArray())
        {
            if (photoElement.ValueKind == JsonValueKind.Object)
            {
                var photo = photoElement.Deserialize<QuotationDamagePhoto>(options);
                if (photo is not null)
                {
                    photos.Add(photo);
                }
            }
        }

        return photos;
    }

    /// <summary>
    /// 將字串型式的圖片欄位轉換為標準照片清單。
    /// </summary>
    private static List<QuotationDamagePhoto> CreatePhotoListFromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<QuotationDamagePhoto>();
        }

        return new List<QuotationDamagePhoto>
        {
            new() { PhotoUid = value.Trim() }
        };
    }

    /// <summary>
    /// 依據字串是否為 null 決定輸出空值或實際內容。
    /// </summary>
    private static void WriteNullableString(Utf8JsonWriter writer, string? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}

/// <summary>
/// 傷痕影像的補充資訊，可記錄拍攝角度或描述說明。
/// </summary>
public class QuotationDamagePhoto
{
    /// <summary>
    /// 舊版欄位，改為傳遞 PhotoUID，保留以相容既有流程。
    /// </summary>
    [Obsolete("請改用 PhotoUid 傳遞圖片識別碼。")]
    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? File { get; set; }

    /// <summary>
    /// 圖片唯一識別碼，對應圖片上傳 API 回傳的 PhotoUID。
    /// </summary>
    [JsonPropertyName("photoUid")]
    public string? PhotoUid { get; set; }

    /// <summary>
    /// 圖片描述，說明拍攝角度或重點標註。
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 是否為主要展示圖片，可協助前端挑選封面影像。
    /// </summary>
    [JsonPropertyName("isPrimary")]
    public bool? IsPrimary { get; set; }
}

/// <summary>
/// 類別金額資訊，包含傷痕金額小計、其他費用與折扣。
/// </summary>
public class QuotationCategoryAmount
{
    /// <summary>
    /// 傷痕預估金額的小計。
    /// </summary>
    public decimal? DamageSubtotal { get; set; }

    /// <summary>
    /// 類別其他費用，例如耗材費或代步車。
    /// </summary>
    public decimal? AdditionalFee { get; set; }

    /// <summary>
    /// 折扣趴數，單位為百分比。
    /// </summary>
    public decimal? DiscountPercentage { get; set; }

    /// <summary>
    /// 折扣原因說明，方便稽核。
    /// </summary>
    public string? DiscountReason { get; set; }
}

/// <summary>
/// 全部類別的金額總覽，提供總金額與零頭折扣資訊。
/// </summary>
public class QuotationCategoryTotal
{
    /// <summary>
    /// 各類別金額小計列表，鍵值對應類別名稱（例如 dent、paint）。
    /// </summary>
    public Dictionary<string, decimal?> CategorySubtotals { get; set; } = new();

    /// <summary>
    /// 零頭折扣金額。
    /// </summary>
    public decimal? RoundingDiscount { get; set; }
}

/// <summary>
/// 車體確認單資料，包含標註後的車身圖片與客戶簽名。
/// </summary>
public class QuotationCarBodyConfirmation
{
    /// <summary>
    /// 舊版欄位，仍保留字串路徑以兼容舊資料。
    /// </summary>
    [Obsolete("車體確認單已移除標註圖片欄位，僅保留簽名影像。")]
    public string? AnnotatedImage { get; set; }

    /// <summary>
    /// 車體受損標記列表，透過座標與損傷類型記錄於車身示意圖。
    /// </summary>
    public List<QuotationCarBodyDamageMarker> DamageMarkers { get; set; } = new();

    /// <summary>
    /// 舊版欄位，已改為 PhotoUID，保留避免破壞相容性。
    /// </summary>
    [Obsolete("請改用 SignaturePhotoUid 傳遞圖片識別碼。")]
    public string? Signature { get; set; }

    /// <summary>
    /// 客戶簽名影像的 PhotoUID，為目前唯一需要綁定的影像欄位。
    /// </summary>
    public string? SignaturePhotoUid { get; set; }
}

/// <summary>
/// 車體確認單的檢查項目，紀錄車身部位與核對狀態。
/// </summary>
public class QuotationCarBodyChecklistItem
{
    /// <summary>
    /// 檢查部位或面板名稱，例如左前門或後保桿。
    /// </summary>
    public string? Part { get; set; }

    /// <summary>
    /// 檢查結果或狀態描述，例如正常、待修或已處理。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 相關備註，可記錄異常說明或維修建議。
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 單一檢查項目的補充圖片，需傳入 PhotoUID 以便後端綁定。
    /// </summary>
    public List<string> Photos { get; set; } = new();
}

/// <summary>
/// 車體受損標記資訊，透過座標定位並標記損傷類型。
/// </summary>
public class QuotationCarBodyDamageMarker
{
    /// <summary>
    /// 標記在示意圖上的 X 座標（0~1），用於呈現水平位置。
    /// </summary>
    public double? X { get; set; }

    /// <summary>
    /// 標記在示意圖上的 Y 座標（0~1），用於呈現垂直位置。
    /// </summary>
    public double? Y { get; set; }

    /// <summary>
    /// 是否為凹痕，對應前端勾選的凹痕類型。
    /// </summary>
    public bool HasDent { get; set; }

    /// <summary>
    /// 是否為刮痕，對應前端勾選的刮痕類型。
    /// </summary>
    public bool HasScratch { get; set; }

    /// <summary>
    /// 是否為掉漆，對應前端勾選的掉漆類型。
    /// </summary>
    public bool HasPaintPeel { get; set; }

    /// <summary>
    /// 補充備註，協助記錄詳細損傷描述。
    /// </summary>
    public string? Remark { get; set; }
}

