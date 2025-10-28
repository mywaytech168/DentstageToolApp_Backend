using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Models.MaintenanceOrders;
using DentstageToolApp.Api.Models.Quotations;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentstageToolApp.Api.Services.Shared;

/// <summary>
/// 摘要資料整理協助工具，提供估價單與維修單列表的轉換邏輯。
/// </summary>
internal static class SummaryMappingHelper
{
    /// <summary>
    /// 將報價單實體集合轉為前端需要的摘要資料。
    /// </summary>
    public static async Task<IReadOnlyList<QuotationSummaryResponse>> BuildQuotationSummariesAsync(
        DentstageToolAppContext dbContext,
        IReadOnlyList<Quatation> quotations,
        CancellationToken cancellationToken)
    {
        // 若沒有資料，直接回傳空集合避免後續流程多做計算。
        if (quotations.Count == 0)
        {
            return Array.Empty<QuotationSummaryResponse>();
        }

        // ---------- 收集必要的參考鍵值 ----------
        var brandUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modelUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var storeUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var technicianUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var quotation in quotations)
        {
            var normalizedBrandUid = NormalizeOptionalText(quotation.BrandUid);
            if (normalizedBrandUid is not null)
            {
                brandUids.Add(normalizedBrandUid);
            }

            var normalizedModelUid = NormalizeOptionalText(quotation.ModelUid);
            if (normalizedModelUid is not null)
            {
                modelUids.Add(normalizedModelUid);
            }

            var normalizedStoreUid = NormalizeOptionalText(quotation.StoreUid);
            if (normalizedStoreUid is not null)
            {
                storeUids.Add(normalizedStoreUid);
            }

            var normalizedTechnicianUid = NormalizeOptionalText(quotation.EstimationTechnicianUid);
            if (normalizedTechnicianUid is not null)
            {
                technicianUids.Add(normalizedTechnicianUid);
            }

            var normalizedCreatorUid = NormalizeOptionalText(quotation.CreatorTechnicianUid);
            if (normalizedCreatorUid is not null)
            {
                technicianUids.Add(normalizedCreatorUid);
            }

            var normalizedUserUid = NormalizeOptionalText(quotation.UserUid);
            if (normalizedUserUid is not null)
            {
                userUids.Add(normalizedUserUid);
            }
        }

        // ---------- 載入參考資料 ----------
        var brandMap = await LoadBrandNamesAsync(dbContext, brandUids, cancellationToken);
        var modelMap = await LoadModelNamesAsync(dbContext, modelUids, cancellationToken);
        var storeMap = await LoadStoreNamesAsync(dbContext, storeUids, cancellationToken);
        var technicianMap = await LoadTechnicianNamesAsync(dbContext, technicianUids, cancellationToken);
        var userMap = await LoadUserDisplayNamesAsync(dbContext, userUids, cancellationToken);

        // ---------- 建立摘要資料 ----------
        var summaries = new List<QuotationSummaryResponse>(quotations.Count);
        foreach (var quotation in quotations)
        {
            var normalizedUserUid = NormalizeOptionalText(quotation.UserUid);
            var normalizedEstimatorUid = NormalizeOptionalText(quotation.EstimationTechnicianUid)
                ?? normalizedUserUid;
            var normalizedCreatorUid = NormalizeOptionalText(quotation.CreatorTechnicianUid);

            var estimatorName = LookupName(normalizedEstimatorUid, technicianMap)
                ?? LookupName(normalizedUserUid, userMap)
                ?? NormalizeOptionalText(quotation.UserName);

            var creatorName = LookupName(normalizedCreatorUid, technicianMap)
                ?? NormalizeOptionalText(quotation.CreatedBy);

            var normalizedBrandUid = NormalizeOptionalText(quotation.BrandUid);
            var normalizedModelUid = NormalizeOptionalText(quotation.ModelUid);
            var normalizedStoreUid = NormalizeOptionalText(quotation.StoreUid);

            summaries.Add(new QuotationSummaryResponse
            {
                QuotationNo = quotation.QuotationNo,
                FixType = QuotationDamageFixTypeHelper.ResolveDisplayName(quotation.FixType),
                Status = quotation.Status,
                CustomerName = quotation.Name,
                CustomerPhone = quotation.Phone,
                CarBrand = LookupName(normalizedBrandUid, brandMap) ?? NormalizeOptionalText(quotation.Brand),
                CarModel = LookupName(normalizedModelUid, modelMap) ?? NormalizeOptionalText(quotation.Model),
                CarPlateNumber = quotation.CarNo,
                EstimationTechnicianUid = normalizedEstimatorUid,
                CreatorTechnicianUid = normalizedCreatorUid,
                StoreName = LookupName(normalizedStoreUid, storeMap)
                    ?? NormalizeOptionalText(quotation.CurrentStatusUser),
                EstimationTechnicianName = estimatorName,
                CreatorTechnicianName = creatorName,
                CreatedAt = quotation.CreationTimestamp
            });
        }

        return summaries;
    }

    /// <summary>
    /// 將維修單實體集合轉換為摘要輸出。
    /// </summary>
    public static async Task<IReadOnlyList<MaintenanceOrderSummaryResponse>> BuildMaintenanceSummariesAsync(
        DentstageToolAppContext dbContext,
        IReadOnlyList<Order> orders,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return Array.Empty<MaintenanceOrderSummaryResponse>();
        }

        // ---------- 收集關聯鍵值 ----------
        var storeUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var technicianUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var order in orders)
        {
            var normalizedStoreUid = NormalizeOptionalText(order.StoreUid);
            if (normalizedStoreUid is not null)
            {
                storeUids.Add(normalizedStoreUid);
            }

            var normalizedEstimatorUid = NormalizeOptionalText(order.EstimationTechnicianUid);
            if (normalizedEstimatorUid is not null)
            {
                technicianUids.Add(normalizedEstimatorUid);
            }

            var normalizedCreatorUid = NormalizeOptionalText(order.CreatorTechnicianUid);
            if (normalizedCreatorUid is not null)
            {
                technicianUids.Add(normalizedCreatorUid);
            }

            var normalizedUserUid = NormalizeOptionalText(order.UserUid);
            if (normalizedUserUid is not null)
            {
                userUids.Add(normalizedUserUid);
            }
        }

        var storeMap = await LoadStoreNamesAsync(dbContext, storeUids, cancellationToken);
        var technicianMap = await LoadTechnicianNamesAsync(dbContext, technicianUids, cancellationToken);
        var userMap = await LoadUserDisplayNamesAsync(dbContext, userUids, cancellationToken);

        var summaries = new List<MaintenanceOrderSummaryResponse>(orders.Count);
        foreach (var order in orders)
        {
            var normalizedStoreUid = NormalizeOptionalText(order.StoreUid);
            var normalizedEstimatorUid = NormalizeOptionalText(order.EstimationTechnicianUid)
                ?? NormalizeOptionalText(order.UserUid);
            var normalizedCreatorUid = NormalizeOptionalText(order.CreatorTechnicianUid);
            var normalizedUserUid = NormalizeOptionalText(order.UserUid);

            var estimatorName = LookupName(normalizedEstimatorUid, technicianMap)
                ?? LookupName(normalizedUserUid, userMap)
                ?? NormalizeOptionalText(order.UserName)
                ?? NormalizeOptionalText(order.CreatedBy);

            var creatorName = LookupName(normalizedCreatorUid, technicianMap)
                ?? NormalizeOptionalText(order.CreatedBy)
                ?? NormalizeOptionalText(order.UserName);

            summaries.Add(new MaintenanceOrderSummaryResponse
            {
                OrderUid = order.OrderUid,
                OrderNo = order.OrderNo,
                Status = order.Status,
                CustomerName = order.Name,
                Phone = order.Phone,
                CarBrand = order.Brand,
                CarModel = order.Model,
                CarPlate = order.CarNo,
                EstimationTechnicianUid = normalizedEstimatorUid,
                CreatorTechnicianUid = normalizedCreatorUid,
                StoreName = LookupName(normalizedStoreUid, storeMap) ?? normalizedStoreUid,
                EstimationTechnicianName = estimatorName,
                CreatorTechnicianName = creatorName,
                CreatedAt = order.CreationTimestamp
            });
        }

        return summaries;
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 載入品牌名稱對照表，減少多次資料庫查詢。
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadBrandNamesAsync(
        DentstageToolAppContext dbContext,
        HashSet<string> brandUids,
        CancellationToken cancellationToken)
    {
        if (brandUids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var entities = await dbContext.Brands
            .AsNoTracking()
            .Where(entity => brandUids.Contains(entity.BrandUid))
            .Select(entity => new { entity.BrandUid, entity.BrandName })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            var uid = NormalizeOptionalText(entity.BrandUid);
            var name = NormalizeOptionalText(entity.BrandName);
            if (uid is null || name is null)
            {
                continue;
            }

            map[uid] = name;
        }

        return map;
    }

    /// <summary>
    /// 載入車型名稱對照表。
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadModelNamesAsync(
        DentstageToolAppContext dbContext,
        HashSet<string> modelUids,
        CancellationToken cancellationToken)
    {
        if (modelUids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var entities = await dbContext.Models
            .AsNoTracking()
            .Where(entity => modelUids.Contains(entity.ModelUid))
            .Select(entity => new { entity.ModelUid, entity.ModelName })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            var uid = NormalizeOptionalText(entity.ModelUid);
            var name = NormalizeOptionalText(entity.ModelName);
            if (uid is null || name is null)
            {
                continue;
            }

            map[uid] = name;
        }

        return map;
    }

    /// <summary>
    /// 載入門市名稱對照表，供摘要顯示。
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadStoreNamesAsync(
        DentstageToolAppContext dbContext,
        HashSet<string> storeUids,
        CancellationToken cancellationToken)
    {
        if (storeUids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var entities = await dbContext.Stores
            .AsNoTracking()
            .Where(entity => storeUids.Contains(entity.StoreUid))
            .Select(entity => new { entity.StoreUid, entity.StoreName })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            var uid = NormalizeOptionalText(entity.StoreUid);
            var name = NormalizeOptionalText(entity.StoreName);
            if (uid is null || name is null)
            {
                continue;
            }

            map[uid] = name;
        }

        return map;
    }

    /// <summary>
    /// 載入技師名稱對照表，支援 UID 與顯示名稱對應。
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadTechnicianNamesAsync(
        DentstageToolAppContext dbContext,
        HashSet<string> technicianUids,
        CancellationToken cancellationToken)
    {
        if (technicianUids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var entities = await dbContext.Technicians
            .AsNoTracking()
            .Where(entity => technicianUids.Contains(entity.TechnicianUid))
            .Select(entity => new { entity.TechnicianUid, entity.TechnicianName })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            var uid = NormalizeOptionalText(entity.TechnicianUid);
            var name = NormalizeOptionalText(entity.TechnicianName);
            if (uid is null || name is null)
            {
                continue;
            }

            map[uid] = name;
        }

        return map;
    }

    /// <summary>
    /// 載入使用者顯示名稱對照表，提供估價與維修資料的名稱補齊。
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadUserDisplayNamesAsync(
        DentstageToolAppContext dbContext,
        HashSet<string> userUids,
        CancellationToken cancellationToken)
    {
        if (userUids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var entities = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(entity => userUids.Contains(entity.UserUid))
            .Select(entity => new { entity.UserUid, entity.DisplayName })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            var uid = NormalizeOptionalText(entity.UserUid);
            var name = NormalizeOptionalText(entity.DisplayName);
            if (uid is null || name is null)
            {
                continue;
            }

            map[uid] = name;
        }

        return map;
    }

    /// <summary>
    /// 取得字典中的對應名稱，若未找到則回傳 null。
    /// </summary>
    private static string? LookupName(string? key, IReadOnlyDictionary<string, string> map)
    {
        if (key is null)
        {
            return null;
        }

        return map.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// 正規化可選文字欄位，移除前後空白並將空字串轉換為 null。
    /// </summary>
    private static string? NormalizeOptionalText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }
}
