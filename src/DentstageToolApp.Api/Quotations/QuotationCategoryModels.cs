using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    /// 傷痕細項列表，紀錄各位置的照片與估價金額。
    /// </summary>
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
    /// 傷痕照片的檔案識別，通常為檔名或外部儲存的 URL。
    /// </summary>
    public string? Photo { get; set; }

    /// <summary>
    /// 傷痕所在位置描述，例如左前門或後保桿。
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// 凹痕狀況或受損程度描述。
    /// </summary>
    public string? DentStatus { get; set; }

    /// <summary>
    /// 備註或補充說明，可記錄施工方式。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 該傷痕預估的施工金額。
    /// </summary>
    public decimal? EstimatedAmount { get; set; }
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
    /// 已標註受損位置的車身圖片，可為檔案識別或 URL。
    /// </summary>
    public string? AnnotatedImage { get; set; }

    /// <summary>
    /// 客戶簽名影像資料。
    /// </summary>
    public string? Signature { get; set; }
}

