using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Stores;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Store;

/// <summary>
/// 門市維運服務實作，處理門市主檔的新增、更新與刪除流程。
/// </summary>
public class StoreManagementService : IStoreManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<StoreManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public StoreManagementService(DentstageToolAppContext dbContext, ILogger<StoreManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateStoreResponse> CreateStoreAsync(CreateStoreRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new StoreManagementException(HttpStatusCode.BadRequest, "請提供門市建立資料。");
        }

        // ---------- 參數整理區 ----------
        var storeName = NormalizeRequiredText(request.StoreName, "門市名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var duplicate = await _dbContext.Stores
            .AsNoTracking()
            .AnyAsync(store => store.StoreName == storeName, cancellationToken);

        if (duplicate)
        {
            throw new StoreManagementException(HttpStatusCode.Conflict, "門市名稱已存在，請勿重複建立。");
        }

        // ---------- 實體建立區 ----------
        var entity = new Infrastructure.Entities.Store
        {
            StoreUid = BuildStoreUid(),
            StoreName = storeName
        };

        await _dbContext.Stores.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增門市 {StoreUid} ({StoreName}) 成功。", operatorLabel, entity.StoreUid, entity.StoreName);

        // ---------- 組裝回應區 ----------
        return new CreateStoreResponse
        {
            StoreUid = entity.StoreUid,
            StoreName = entity.StoreName,
            Message = "已建立門市資料。"
        };
    }

    /// <inheritdoc />
    public async Task<EditStoreResponse> EditStoreAsync(EditStoreRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new StoreManagementException(HttpStatusCode.BadRequest, "請提供門市編輯資料。");
        }

        // ---------- 參數整理區 ----------
        var storeUid = NormalizeRequiredText(request.StoreUid, "門市識別碼");
        var storeName = NormalizeRequiredText(request.StoreName, "門市名稱");
        var operatorLabel = NormalizeOperator(operatorName);

        var entity = await _dbContext.Stores
            .FirstOrDefaultAsync(store => store.StoreUid == storeUid, cancellationToken);

        if (entity is null)
        {
            throw new StoreManagementException(HttpStatusCode.NotFound, "找不到對應的門市資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duplicate = await _dbContext.Stores
            .AsNoTracking()
            .AnyAsync(store => store.StoreUid != storeUid && store.StoreName == storeName, cancellationToken);

        if (duplicate)
        {
            throw new StoreManagementException(HttpStatusCode.Conflict, "門市名稱已存在於其他門市，請重新命名。");
        }

        // ---------- 實體更新區 ----------
        entity.StoreName = storeName;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        _logger.LogInformation("操作人員 {Operator} 編輯門市 {StoreUid} ({StoreName}) 成功。", operatorLabel, entity.StoreUid, entity.StoreName);

        // ---------- 組裝回應區 ----------
        return new EditStoreResponse
        {
            StoreUid = entity.StoreUid,
            StoreName = entity.StoreName,
            UpdatedAt = now,
            Message = "已更新門市資料。"
        };
    }

    /// <inheritdoc />
    public async Task<DeleteStoreResponse> DeleteStoreAsync(DeleteStoreRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new StoreManagementException(HttpStatusCode.BadRequest, "請提供門市刪除資料。");
        }

        // ---------- 參數整理區 ----------
        var storeUid = NormalizeRequiredText(request.StoreUid, "門市識別碼");
        var operatorLabel = NormalizeOperator(operatorName);

        var entity = await _dbContext.Stores
            .FirstOrDefaultAsync(store => store.StoreUid == storeUid, cancellationToken);

        if (entity is null)
        {
            throw new StoreManagementException(HttpStatusCode.NotFound, "找不到對應的門市資料，請確認識別碼是否正確。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        var hasTechnicians = await _dbContext.Technicians
            .AsNoTracking()
            .AnyAsync(technician => technician.StoreUid == storeUid, cancellationToken);

        if (hasTechnicians)
        {
            throw new StoreManagementException(HttpStatusCode.Conflict, "該門市仍有技師資料，請先調整技師的隸屬門市。");
        }

        var hasQuotations = await _dbContext.Quatations
            .AsNoTracking()
            .AnyAsync(quotation => quotation.StoreUid == storeUid, cancellationToken);

        if (hasQuotations)
        {
            throw new StoreManagementException(HttpStatusCode.Conflict, "該門市仍被報價單使用，請先調整報價單資料後再刪除。");
        }

        // ---------- 實體刪除區 ----------
        _dbContext.Stores.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 刪除門市 {StoreUid} ({StoreName}) 成功。", operatorLabel, entity.StoreUid, entity.StoreName);

        // ---------- 組裝回應區 ----------
        return new DeleteStoreResponse
        {
            StoreUid = entity.StoreUid,
            Message = "已刪除門市資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生門市識別碼，統一使用 St_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildStoreUid()
    {
        return $"St_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 處理必填字串欄位，若為空則丟出例外。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StoreManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 正規化操作人員名稱。
    /// </summary>
    private static string NormalizeOperator(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return "UnknownUser";
        }

        return operatorName.Trim();
    }

    // ---------- 生命週期 ----------
    // 目前無額外生命週期需求，保留區塊以符合專案規範。
}
