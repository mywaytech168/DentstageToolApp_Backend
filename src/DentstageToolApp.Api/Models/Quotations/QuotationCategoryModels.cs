using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DentstageToolApp.Api.Models.Quotations;

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
    private string? _photo;
    private string? _fixType;

    /// <summary>
    /// 主要圖片的 PhotoUID，作為傷痕與照片的唯一對應。所有輸入都會先行去除空白。
    /// </summary>
    [JsonIgnore]
    public string? Photo
    {
        get => _photo;
        set => _photo = NormalizePhotoValue(value);
    }

    /// <summary>
    /// 提供對外序列化使用的 photo 欄位，維持欄位名稱一致並同步寫入內部值。
    /// </summary>
    [JsonPropertyName("photo")]
    public string? DisplayPhoto
    {
        get => Photo;
        set => Photo = value;
    }

    /// <summary>
    /// 舊版欄位「photos」仍可能由前端傳入字串，為維持相容性仍接受並轉換成單一 photo。
    /// </summary>
    [JsonPropertyName("photos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPhotos
    {
        get => null;
        set => Photo = value;
    }

    /// <summary>
    /// 舊版中文欄位「圖片」，解析時同樣導入主要圖片欄位。
    /// </summary>
    [JsonPropertyName("圖片")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyChinesePhoto
    {
        get => null;
        set => Photo = value;
    }

    /// <summary>
    /// 傷痕所屬維修類型中文標籤，維持與舊版資料的相容性。
    /// </summary>
    [JsonPropertyName("fixType")]
    public string? DisplayFixType
    {
        get
        {
            // 若已有預先計算的顯示名稱，直接輸出中文內容。
            if (!string.IsNullOrWhiteSpace(FixTypeName))
            {
                return FixTypeName;
            }

            return string.IsNullOrWhiteSpace(FixType)
                ? null
                : QuotationDamageFixTypeHelper.ResolveDisplayName(FixType);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                FixType = null;
                return;
            }

            var resolved = QuotationDamageFixTypeHelper.ResolveDisplayName(value);
            FixType = resolved;
        }
    }

    /// <summary>
    /// 傷痕所屬維修類型顯示名稱，預設為中文描述供前端呈現。
    /// </summary>
    [JsonIgnore]
    public string? FixTypeName { get; set; }

    /// <summary>
    /// 舊版欄位：維修類型顯示名稱。新流程改以 FixType 單獨輸出中文名稱，因此此欄位僅保留解析舊資料用途。
    /// </summary>
    [JsonPropertyName("fixTypeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyFixTypeName
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                FixType = value;
            }
        }
    }

    /// <summary>
    /// 舊版 JSON 的中文欄位，避免歷史資料解析失敗。
    /// </summary>
    [JsonPropertyName("維修類型")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyChineseFixType
    {
        get => null;
        set => FixType = value;
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
    /// 內部使用的維修類型識別值，提供轉換器進行分類並保留既有資料相容性。
    /// </summary>
    [JsonIgnore]
    public string? FixType
    {
        get => _fixType;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _fixType = null;
                FixTypeName = null;
            }
            else
            {
                var resolved = QuotationDamageFixTypeHelper.ResolveDisplayName(value);
                _fixType = resolved;
                FixTypeName = resolved;
            }
        }
    }

    /// <summary>
    /// 將輸入的照片識別碼正規化，統一去除頭尾空白並處理空字串情境。
    /// </summary>
    private static string? NormalizePhotoValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

            if (LooksLikeSingleDamage(root))
            {
                var item = ParseDamageItem(root, options);
                return item is null
                    ? new List<QuotationDamageItem>()
                    : new List<QuotationDamageItem> { item };
            }

            return ParseGroupedDamages(root, options);
        }

        throw new JsonException("傷痕資料格式不符，請提供物件或陣列。");
    }

    /// <summary>
    /// 序列化時依維修類型切分陣列，輸出凹痕、美容、鈑烤與其他四大群組。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, List<QuotationDamageItem> value, JsonSerializerOptions options)
    {
        // 最新規格改為僅輸出傷痕陣列，讓前端僅需讀取 FixType 即可判斷中文維修類型。
        writer.WriteStartArray();

        if (value is { Count: > 0 })
        {
            foreach (var damage in value)
            {
                WriteSingleDamage(writer, damage, options);
            }
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 將單一傷痕輸出為前端期待的物件格式，並包含維修類型資訊。
    /// </summary>
    private static void WriteSingleDamage(Utf8JsonWriter writer, QuotationDamageItem? item, JsonSerializerOptions options)
    {
        _ = options; // 目前輸出僅需主要圖片識別碼，因此未使用額外序列化選項。
        var target = item ?? new QuotationDamageItem();
        QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(target);

        writer.WriteStartObject();

        writer.WritePropertyName("photo");
        var primaryPhotoUid = NormalizePhoto(target.Photo);
        if (!string.IsNullOrWhiteSpace(primaryPhotoUid))
        {
            writer.WriteStringValue(primaryPhotoUid!);
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

        writer.WritePropertyName("fixType");
        WriteNullableString(writer, target.DisplayFixType);

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
                QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(item);
                return item;
            }
        }
        catch (JsonException)
        {
            // 舊版資料在「圖片」欄位可能以字串傳遞，導致序列化失敗，此處改以人工映射處理。
        }

        // 以逐欄位讀取方式手動建立資料，確保舊版欄位仍可被解析。
        var fallback = new QuotationDamageItem
        {
            DisplayPhoto = ReadPhotoValue(element),
            DisplayPosition = ReadString(element, "position", "位置"),
            DisplayDentStatus = ReadString(element, "dentStatus", "凹痕狀況"),
            DisplayDescription = ReadString(element, "description", "說明"),
            DisplayEstimatedAmount = ReadDecimal(element, "estimatedAmount", "預估金額"),
            DisplayFixType = ReadString(element, "fixType", "維修類型"),
            FixTypeName = ReadString(element, "fixTypeName")
        };

        QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(fallback);
        return fallback;
    }

    /// <summary>
    /// 解析分組格式的傷痕資料，將各維修類型陣列攤平成統一集合。
    /// </summary>
    private static List<QuotationDamageItem> ParseGroupedDamages(JsonElement root, JsonSerializerOptions options)
    {
        var result = new List<QuotationDamageItem>();

        foreach (var property in root.EnumerateObject())
        {
            var normalizedKey = QuotationDamageFixTypeHelper.Normalize(property.Name)
                ?? QuotationDamageFixTypeHelper.ResolveDisplayName(property.Name);

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in property.Value.EnumerateArray())
                {
                    var item = ParseDamageItem(element, options);
                    if (item is null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.FixType))
                    {
                        item.FixType = normalizedKey;
                    }

                    QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(item, normalizedKey);
                    result.Add(item);
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var single = ParseDamageItem(property.Value, options);
                if (single is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(single.FixType))
                {
                    single.FixType = normalizedKey;
                }

                QuotationDamageFixTypeHelper.EnsureFixTypeDefaults(single, normalizedKey);
                result.Add(single);
            }
        }

        return result;
    }

    /// <summary>
    /// 判斷物件是否為單筆傷痕資料，而非分組陣列包裝。
    /// </summary>
    private static bool LooksLikeSingleDamage(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var knownFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "photo",
            "photos",
            "position",
            "dentStatus",
            "description",
            "estimatedAmount",
            "fixType",
            "fixTypeName",
            "圖片",
            "位置",
            "凹痕狀況",
            "說明",
            "預估金額",
            "維修類型"
        };

        foreach (var property in element.EnumerateObject())
        {
            if (!knownFields.Contains(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 讀取圖片欄位，若為陣列或物件則取第一筆 PhotoUID，維持與單一欄位一致。
    /// </summary>
    private static string? ReadPhotoValue(JsonElement root)
    {
        if (!TryGetProperty(root, out var element, "photo", "photos", "圖片"))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => NormalizePhoto(element.GetString()),
            JsonValueKind.Object => ReadPhotoFromObject(element),
            JsonValueKind.Array => ReadPhotoFromArray(element),
            _ => null
        };
    }

    /// <summary>
    /// 從物件欄位中擷取 PhotoUID，支援 photoUid 與 file 兩種欄位名稱。
    /// </summary>
    private static string? ReadPhotoFromObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty("photoUid", out var uidElement) && uidElement.ValueKind == JsonValueKind.String)
        {
            return NormalizePhoto(uidElement.GetString());
        }

        if (element.TryGetProperty("file", out var fileElement) && fileElement.ValueKind == JsonValueKind.String)
        {
            return NormalizePhoto(fileElement.GetString());
        }

        return null;
    }

    /// <summary>
    /// 讀取陣列形式的圖片欄位，回傳第一筆有效的 PhotoUID。
    /// </summary>
    private static string? ReadPhotoFromArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in element.EnumerateArray())
        {
            var value = item.ValueKind switch
            {
                JsonValueKind.String => NormalizePhoto(item.GetString()),
                JsonValueKind.Object => ReadPhotoFromObject(item),
                _ => null
            };

            if (value is not null)
            {
                return value;
            }
        }

        return null;
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

    /// <summary>
    /// 正規化圖片識別碼字串，避免包含空白或空值。
    /// </summary>
    private static string? NormalizePhoto(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// 傷痕維修類型的共用工具，負責鍵值正規化與顯示文字處理。
/// </summary>
internal static class QuotationDamageFixTypeHelper
{
    // 內部維護的固定順序清單，避免每次呼叫都重新建立 List 實例。
    private static readonly List<string> CanonicalOrderList = new() { "凹痕", "美容", "板烤", "其他" };

    private static readonly HashSet<string> CanonicalSet = new(StringComparer.Ordinal)
    {
        "凹痕",
        "美容",
        "板烤",
        "其他"
    };

    /// <summary>
    /// 維修類型輸出的固定順序，確保前端畫面呈現一致。
    /// </summary>
    public static IReadOnlyList<string> CanonicalOrder => CanonicalOrderList;

    /// <summary>
    /// 正規化維修類型字串，僅接受中文標籤，其他內容會回傳 null 交由外部決定是否改為「其他」。
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return CanonicalSet.Contains(trimmed) ? trimmed : null;
    }

    /// <summary>
    /// 針對輸出提供中文顯示文字，缺省或無法識別時直接回傳「其他」。
    /// </summary>
    public static string ResolveDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "其他";
        }

        var normalized = Normalize(value);
        return normalized ?? "其他";
    }

    /// <summary>
    /// 計算維修類型於固定順序中的索引位置，用於排序時維持既定順序。
    /// </summary>
    public static int ResolveOrderIndex(string? value)
    {
        // 先嘗試正規化字串，若失敗則改用顯示名稱避免 null 影響排序。
        var normalized = Normalize(value) ?? ResolveDisplayName(value);
        var index = CanonicalOrderList.IndexOf(normalized);
        return index >= 0 ? index : int.MaxValue;
    }

    /// <summary>
    /// 依據現有值決定群組索引，若無法判斷則落在「其他」。
    /// </summary>
    public static string DetermineGroupKey(string? value)
    {
        return Normalize(value) ?? "其他";
    }

    /// <summary>
    /// 確保傷痕物件具備正規化後的維修類型與顯示名稱。
    /// </summary>
    public static void EnsureFixTypeDefaults(QuotationDamageItem damage, string? fallback = null)
    {
        if (damage is null)
        {
            return;
        }

        var normalized = Normalize(damage.FixType);
        if (normalized is null && !string.IsNullOrWhiteSpace(damage.FixTypeName))
        {
            normalized = Normalize(damage.FixTypeName);
        }
        if (normalized is null)
        {
            normalized = Normalize(fallback);
        }

        if (normalized is null && !string.IsNullOrWhiteSpace(damage.FixType))
        {
            normalized = ResolveDisplayName(damage.FixType);
        }
        if (normalized is null && !string.IsNullOrWhiteSpace(damage.FixTypeName))
        {
            normalized = Normalize(damage.FixTypeName) ?? ResolveDisplayName(damage.FixTypeName);
        }
        if (normalized is null)
        {
            normalized = Normalize(fallback);
        }
        if (normalized is null && !string.IsNullOrWhiteSpace(fallback))
        {
            normalized = ResolveDisplayName(fallback);
        }

        if (normalized is null)
        {
            damage.FixType = null;
            damage.FixTypeName = null;
            return;
        }

        damage.FixType = normalized;
        damage.FixTypeName = normalized;
        // 透過自動補齊顯示名稱，外部即可僅依 FixType 呈現中文維修類型。
    }

    /// <summary>
    /// 針對簡化輸出的傷痕摘要套用維修類型預設值。
    /// </summary>
    public static void EnsureFixTypeDefaults(QuotationDamageSummary summary, string? fallback = null)
    {
        if (summary is null)
        {
            return;
        }

        var normalized = Normalize(summary.FixType);
        if (normalized is null && !string.IsNullOrWhiteSpace(summary.FixTypeName))
        {
            normalized = Normalize(summary.FixTypeName);
        }
        if (normalized is null)
        {
            normalized = Normalize(fallback);
        }

        if (normalized is null && !string.IsNullOrWhiteSpace(summary.FixType))
        {
            normalized = ResolveDisplayName(summary.FixType);
        }
        if (normalized is null && !string.IsNullOrWhiteSpace(summary.FixTypeName))
        {
            normalized = Normalize(summary.FixTypeName) ?? ResolveDisplayName(summary.FixTypeName);
        }
        if (normalized is null)
        {
            normalized = Normalize(fallback);
        }
        if (normalized is null && !string.IsNullOrWhiteSpace(fallback))
        {
            normalized = ResolveDisplayName(fallback);
        }

        if (normalized is null)
        {
            summary.FixType = null;
            summary.FixTypeName = null;
            return;
        }

        summary.FixType = normalized;
        summary.FixTypeName = normalized;
        // 確保摘要輸出時維持中文顯示同步，提供前端直接使用。
    }
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

