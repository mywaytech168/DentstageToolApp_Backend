using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
    /// </summary>
    [JsonConverter(typeof(QuotationDamageCollectionConverter))]
    public List<QuotationDamageItem> Damages { get; set; } = new();

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
    public string? Photo { get; set; }

    /// <summary>
    /// 舊欄位別名，允許仍以 photo 傳值，但輸出時隱藏英文欄位。
    /// </summary>
    [JsonPropertyName("photo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPhoto
    {
        get => null;
        set => Photo = value;
    }

    /// <summary>
    /// 內部使用的圖片清單，作為舊欄位與新欄位的共用儲存。
    /// </summary>
    [JsonIgnore]
    public List<QuotationDamagePhoto> Photos { get; set; } = new();

    /// <summary>
    /// 新欄位：提供前端顯示「圖片」欄位使用，並將資料寫入共用清單。
    /// </summary>
    [JsonPropertyName("圖片")]
    public List<QuotationDamagePhoto> DisplayPhotos
    {
        get => Photos;
        set => Photos = value ?? new List<QuotationDamagePhoto>();
    }

    /// <summary>
    /// 舊欄位對應，仍允許前端傳入 photos 以保留相容性，輸出時隱藏。
    /// </summary>
    [JsonPropertyName("photos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<QuotationDamagePhoto>? LegacyPhotos
    {
        get => null;
        set
        {
            // 舊欄位仍可寫入，若為 null 則建立空集合以免殘留舊資料。
            Photos = value ?? new List<QuotationDamagePhoto>();
        }
    }

    /// <summary>
    /// 內部使用的位置字串，供新舊欄位共用。
    /// </summary>
    [JsonIgnore]
    public string? Position { get; set; }

    /// <summary>
    /// 新欄位：提供中文欄位名稱「位置」，方便前端直接對應表格欄位。
    /// </summary>
    [JsonPropertyName("位置")]
    public string? DisplayPosition
    {
        get => Position;
        set => Position = value;
    }

    /// <summary>
    /// 舊欄位位置，仍接受 position 以兼容舊版請求，序列化時不輸出。
    /// </summary>
    [JsonPropertyName("position")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPosition
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
    /// 新欄位：前端顯示中文「凹痕狀況」，與舊資料共用同一來源。
    /// </summary>
    [JsonPropertyName("凹痕狀況")]
    public string? DisplayDentStatus
    {
        get => DentStatus;
        set => DentStatus = value;
    }

    /// <summary>
    /// 舊欄位：接受 dentStatus，輸出時不顯示英文欄位。
    /// </summary>
    [JsonPropertyName("dentStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyDentStatus
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
    /// 新欄位：中文欄位名稱「說明」，提供表單直接綁定。
    /// </summary>
    [JsonPropertyName("說明")]
    public string? DisplayDescription
    {
        get => Description;
        set => Description = value;
    }

    /// <summary>
    /// 舊欄位：接受 description，避免舊版呼叫失效。
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyDescription
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
    /// 新欄位：中文欄位名稱「預估金額」，傳入時同步寫入內部欄位。
    /// </summary>
    [JsonPropertyName("預估金額")]
    public decimal? DisplayEstimatedAmount
    {
        get => EstimatedAmount;
        set => EstimatedAmount = value;
    }

    /// <summary>
    /// 舊欄位：接受 estimatedAmount，確保與舊版資料格式相容。
    /// </summary>
    [JsonPropertyName("estimatedAmount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LegacyEstimatedAmount
    {
        get => null;
        set => EstimatedAmount = value;
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
                var item = element.Deserialize<QuotationDamageItem>(options);
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
            var item = new QuotationDamageItem
            {
                DisplayPhotos = ReadPhotoList(root, options),
                DisplayPosition = ReadString(root, "位置", "position"),
                DisplayDentStatus = ReadString(root, "凹痕狀況", "dentStatus"),
                DisplayDescription = ReadString(root, "說明", "description"),
                DisplayEstimatedAmount = ReadDecimal(root, "預估金額", "estimatedAmount")
            };

            return new List<QuotationDamageItem> { item };
        }

        throw new JsonException("傷痕資料格式不符，請提供物件或陣列。");
    }

    /// <summary>
    /// 序列化時優先輸出物件格式，若有多筆資料則回退為陣列避免資訊遺失。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, List<QuotationDamageItem> value, JsonSerializerOptions options)
    {
        if (value is null || value.Count == 0)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        if (value.Count == 1)
        {
            WriteSingleDamage(writer, value[0], options);
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 將單一傷痕輸出為前端期待的物件格式。
    /// </summary>
    private static void WriteSingleDamage(Utf8JsonWriter writer, QuotationDamageItem? item, JsonSerializerOptions options)
    {
        var target = item ?? new QuotationDamageItem();

        writer.WriteStartObject();

        writer.WritePropertyName("圖片");
        JsonSerializer.Serialize(writer, target.DisplayPhotos ?? new List<QuotationDamagePhoto>(), options);

        writer.WritePropertyName("位置");
        WriteNullableString(writer, target.DisplayPosition);

        writer.WritePropertyName("凹痕狀況");
        WriteNullableString(writer, target.DisplayDentStatus);

        writer.WritePropertyName("說明");
        WriteNullableString(writer, target.DisplayDescription);

        writer.WritePropertyName("預估金額");
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
    /// 讀取圖片集合，支援陣列與單一物件寫法。
    /// </summary>
    private static List<QuotationDamagePhoto> ReadPhotoList(JsonElement root, JsonSerializerOptions options)
    {
        if (!TryGetProperty(root, out var element, "圖片", "photos", "photo"))
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
    public string? File { get; set; }

    /// <summary>
    /// 圖片唯一識別碼，對應圖片上傳 API 回傳的 PhotoUID。
    /// </summary>
    public string? PhotoUid { get; set; }

    /// <summary>
    /// 圖片描述，說明拍攝角度或重點標註。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否為主要展示圖片，可協助前端挑選封面影像。
    /// </summary>
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
    /// 舊版欄位，已改為 PhotoUID，保留避免破壞相容性。
    /// </summary>
    [Obsolete("請改用 AnnotatedPhotoUid 傳遞圖片識別碼。")]
    public string? AnnotatedImage { get; set; }

    /// <summary>
    /// 已標註受損位置的車身圖片識別碼。
    /// </summary>
    public string? AnnotatedPhotoUid { get; set; }

    /// <summary>
    /// 車體確認細項列表，可對應檢查部位與勾選結果。
    /// </summary>
    public List<QuotationCarBodyChecklistItem> Checklist { get; set; } = new();

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
    /// 客戶簽名影像的 PhotoUID。
    /// </summary>
    public string? SignaturePhotoUid { get; set; }

    /// <summary>
    /// 多份簽名圖片清單，支援一次上傳多張簽名檔並由後端綁定。
    /// </summary>
    public List<string> SignaturePhotoUids { get; set; } = new();
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

