using System;
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
    /// 傳統版本僅支援單張照片，保留此欄位以維持相容性。
    /// </summary>
    [Obsolete("請改用 Photos 集合傳遞多張傷痕圖片。")]
    public string? Photo { get; set; }

    /// <summary>
    /// 傷痕相關的圖片列表，支援多角度或不同標註的影像。
    /// </summary>
    public List<QuotationDamagePhoto> Photos { get; set; } = new();

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
/// 傷痕影像的補充資訊，可記錄拍攝角度或描述說明。
/// </summary>
public class QuotationDamagePhoto
{
    /// <summary>
    /// 圖片檔案識別或外部儲存 URL。
    /// </summary>
    public string? File { get; set; }

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
    /// 已標註受損位置的車身圖片，可為檔案識別或 URL。
    /// </summary>
    public string? AnnotatedImage { get; set; }

    /// <summary>
    /// 車體確認細項列表，可對應檢查部位與勾選結果。
    /// </summary>
    public List<QuotationCarBodyChecklistItem> Checklist { get; set; } = new();

    /// <summary>
    /// 客戶簽名影像資料。
    /// </summary>
    public string? Signature { get; set; }
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
    /// 單一檢查項目的補充圖片，方便比對局部細節。
    /// </summary>
    public List<string> Photos { get; set; } = new();
}

