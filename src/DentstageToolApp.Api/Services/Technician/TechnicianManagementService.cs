using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.Technicians;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Technician;

/// <summary>
/// 技師維運服務實作，負責處理技師主檔的新增、更新與刪除流程。
/// </summary>
public class TechnicianManagementService : ITechnicianManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<TechnicianManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public TechnicianManagementService(DentstageToolAppContext dbContext, ILogger<TechnicianManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateTechnicianResponse> CreateTechnicianAsync(CreateTechnicianRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new TechnicianManagementException(HttpStatusCode.BadRequest, "請提供技師建立資料。");
        }

        // ---------- 參數整理區 ----------
        var technicianName = NormalizeRequiredText(request.TechnicianName, "技師姓名", 100);
        var jobTitle = NormalizeOptionalText(request.JobTitle, 50);
        var storeUid = NormalizeRequiredText(request.StoreUid, "門市識別碼", 100);
        var operatorLabel = NormalizeOperator(operatorName);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var storeExists = await _dbContext.Stores
            .AsNoTracking()
            .AnyAsync(store => store.StoreUid == storeUid, cancellationToken);

        if (!storeExists)
        {
            throw new TechnicianManagementException(HttpStatusCode.NotFound, "找不到對應的門市資料，請確認識別碼是否正確。");
        }

        var duplicate = await _dbContext.Technicians
            .AsNoTracking()
            .AnyAsync(technician => technician.StoreUid == storeUid && technician.TechnicianName == technicianName, cancellationToken);

        if (duplicate)
        {
            throw new TechnicianManagementException(HttpStatusCode.Conflict, "該門市已存在相同姓名的技師，請重新輸入。");
        }

        // ---------- 實體建立區 ----------
        var entity = new DentstageToolApp.Infrastructure.Entities.Technician
        {
            TechnicianUid = BuildTechnicianUid(),
            TechnicianName = technicianName,
            JobTitle = jobTitle,
            StoreUid = storeUid
        };

        await _dbContext.Technicians.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 新增技師 {TechnicianUid} ({TechnicianName}) 至門市 {StoreUid} 成功。",
            operatorLabel,
            entity.TechnicianUid,
            entity.TechnicianName,
            storeUid);

        // ---------- 組裝回應區 ----------
        return new CreateTechnicianResponse
        {
            TechnicianUid = entity.TechnicianUid,
            TechnicianName = entity.TechnicianName,
            JobTitle = entity.JobTitle,
            StoreUid = entity.StoreUid ?? string.Empty,
            Message = "已建立技師資料。"
        };
    }

    /// <inheritdoc />
    public async Task<EditTechnicianResponse> EditTechnicianAsync(EditTechnicianRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new TechnicianManagementException(HttpStatusCode.BadRequest, "請提供技師編輯資料。");
        }

        // ---------- 參數整理區 ----------
        var technicianUid = NormalizeRequiredText(request.TechnicianUid, "技師識別碼", 100);
        var technicianName = NormalizeRequiredText(request.TechnicianName, "技師姓名", 100);
        var jobTitle = NormalizeOptionalText(request.JobTitle, 50);
        var storeUid = NormalizeRequiredText(request.StoreUid, "門市識別碼", 100);
        var operatorLabel = NormalizeOperator(operatorName);

        var entity = await _dbContext.Technicians
            .FirstOrDefaultAsync(technician => technician.TechnicianUid == technicianUid, cancellationToken);

        if (entity is null)
        {
            throw new TechnicianManagementException(HttpStatusCode.NotFound, "找不到對應的技師資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var storeExists = await _dbContext.Stores
            .AsNoTracking()
            .AnyAsync(store => store.StoreUid == storeUid, cancellationToken);

        if (!storeExists)
        {
            throw new TechnicianManagementException(HttpStatusCode.NotFound, "找不到對應的門市資料，請確認識別碼是否正確。");
        }

        var duplicate = await _dbContext.Technicians
            .AsNoTracking()
            .AnyAsync(technician => technician.TechnicianUid != technicianUid && technician.StoreUid == storeUid && technician.TechnicianName == technicianName, cancellationToken);

        if (duplicate)
        {
            throw new TechnicianManagementException(HttpStatusCode.Conflict, "該門市已存在相同姓名的技師，請重新輸入。");
        }

        // ---------- 實體更新區 ----------
        entity.TechnicianName = technicianName;
        entity.JobTitle = jobTitle;
        entity.StoreUid = storeUid;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        _logger.LogInformation(
            "操作人員 {Operator} 編輯技師 {TechnicianUid} ({TechnicianName}) 成功，隸屬門市 {StoreUid}。",
            operatorLabel,
            entity.TechnicianUid,
            entity.TechnicianName,
            entity.StoreUid);

        // ---------- 組裝回應區 ----------
        return new EditTechnicianResponse
        {
            TechnicianUid = entity.TechnicianUid,
            TechnicianName = entity.TechnicianName,
            JobTitle = entity.JobTitle,
            StoreUid = entity.StoreUid ?? string.Empty,
            UpdatedAt = now,
            Message = "已更新技師資料。"
        };
    }

    /// <inheritdoc />
    public async Task<DeleteTechnicianResponse> DeleteTechnicianAsync(DeleteTechnicianRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new TechnicianManagementException(HttpStatusCode.BadRequest, "請提供技師刪除資料。");
        }

        // ---------- 參數整理區 ----------
        var technicianUid = NormalizeRequiredText(request.TechnicianUid, "技師識別碼", 100);
        var operatorLabel = NormalizeOperator(operatorName);

        var entity = await _dbContext.Technicians
            .FirstOrDefaultAsync(technician => technician.TechnicianUid == technicianUid, cancellationToken);

        if (entity is null)
        {
            throw new TechnicianManagementException(HttpStatusCode.NotFound, "找不到對應的技師資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation => quotation.EstimationTechnicianUid == technicianUid, cancellationToken);

        if (hasQuotations)
        {
            throw new TechnicianManagementException(HttpStatusCode.Conflict, "該技師仍被報價單使用，請先調整資料後再刪除。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.Technicians.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "操作人員 {Operator} 刪除技師 {TechnicianUid} ({TechnicianName}) 成功。",
            operatorLabel,
            entity.TechnicianUid,
            entity.TechnicianName);

        // ---------- 組裝回應區 ----------
        return new DeleteTechnicianResponse
        {
            TechnicianUid = entity.TechnicianUid,
            Message = "已刪除技師資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生技師識別碼，統一使用 Tc_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildTechnicianUid()
    {
        return $"Tc_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 正規化必填字串，並在為空時拋出例外。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TechnicianManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        var trimmed = value.Trim();
        if (maxLength.HasValue && trimmed.Length > maxLength.Value)
        {
            throw new TechnicianManagementException(HttpStatusCode.BadRequest, $"{fieldName}長度不可超過 {maxLength.Value} 個字元，請重新輸入。");
        }

        return trimmed;
    }

    /// <summary>
    /// 正規化可選字串，將空白視為 null，並限制最大長度。
    /// </summary>
    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new TechnicianManagementException(HttpStatusCode.BadRequest, $"欄位長度不可超過 {maxLength} 個字元，請重新輸入。");
        }

        return trimmed;
    }

    /// <summary>
    /// 將操作人員名稱正規化，避免記錄出現空值。
    /// </summary>
    private static string NormalizeOperator(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return "UnknownUser";
        }

        return operatorName.Trim();
    }
}

/// <summary>
/// 技師維運專用例外類別，封裝 HTTP 狀態碼與錯誤訊息。
/// </summary>
public class TechnicianManagementException : Exception
{
    /// <summary>
    /// 建構子，建立包含狀態碼與訊息的例外物件。
    /// </summary>
    public TechnicianManagementException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// 對應的 HTTP 狀態碼，供控制器轉換為 ProblemDetails。
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}
