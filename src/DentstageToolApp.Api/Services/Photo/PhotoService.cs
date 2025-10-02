using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Options;
using DentstageToolApp.Api.Photos;
using DentstageToolApp.Api.Services.Quotation;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DentstageToolApp.Api.Services.Photo;

/// <summary>
/// 照片服務實作，負責處理檔案寫入、讀取與估價單綁定流程。
/// </summary>
public class PhotoService : IPhotoService
{
    private readonly DentstageToolAppContext _context;
    private readonly ILogger<PhotoService> _logger;
    private readonly PhotoStorageOptions _storageOptions;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    /// <summary>
    /// 建構子，注入資料庫內容物件、日誌工具與儲存設定。
    /// </summary>
    public PhotoService(
        DentstageToolAppContext context,
        ILogger<PhotoService> logger,
        IOptions<PhotoStorageOptions> storageOptions)
    {
        _context = context;
        _logger = logger;
        _storageOptions = storageOptions.Value ?? new PhotoStorageOptions();
    }

    // ---------- API 邏輯區 ----------

    /// <inheritdoc />
    public async Task<UploadPhotoResponse> UploadAsync(IFormFile file, string? remark, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "請提供要上傳的圖片檔案。");
        }

        if (file.Length <= 0)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "圖片檔案內容為空，請重新選擇檔案。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var photoUid = GeneratePhotoUid();
        var extension = NormalizeExtension(Path.GetExtension(file.FileName));
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GuessExtension(file.ContentType) ?? ".bin";
        }

        var storageRoot = EnsureStorageRoot();
        var physicalFileName = photoUid + extension;
        var physicalPath = Path.Combine(storageRoot, physicalFileName);

        try
        {
            // ---------- 檔案寫入區 ----------
            await using (var stream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            // ---------- 資料紀錄區 ----------
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? GuessContentType(extension) ?? "application/octet-stream"
                : file.ContentType;

            var metadata = new PhotoMetadata
            {
                OriginalFileName = file.FileName,
                ContentType = contentType,
                FileExtension = extension,
                Remark = NormalizeRemark(remark)
            };

            var entity = new PhotoDatum
            {
                PhotoUid = photoUid,
                QuotationUid = null,
                RelatedUid = null,
                Comment = JsonSerializer.Serialize(metadata, _jsonOptions),
                Posion = metadata.Remark
            };

            await _context.PhotoData.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("成功上傳照片 {PhotoUid}，原始檔名：{FileName}", photoUid, file.FileName);

            return new UploadPhotoResponse
            {
                PhotoUid = photoUid,
                FileName = file.FileName,
                ContentType = metadata.ContentType,
                FileSize = file.Length
            };
        }
        catch
        {
            // 若資料庫寫入失敗需清理已儲存的檔案，避免孤兒檔案累積。
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PhotoFile?> GetAsync(string photoUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeUid(photoUid);
        if (normalizedUid is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var entity = await _context.PhotoData
            .AsNoTracking()
            .FirstOrDefaultAsync(photo => photo.PhotoUid == normalizedUid, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var metadata = ParseMetadata(entity.Comment);
        var extension = metadata?.FileExtension ?? ".bin";
        var storageRoot = EnsureStorageRoot();
        var physicalPath = Path.Combine(storageRoot, normalizedUid + extension);

        if (!File.Exists(physicalPath))
        {
            _logger.LogWarning("找不到照片 {PhotoUid} 的實體檔案，路徑：{Path}", normalizedUid, physicalPath);
            return null;
        }

        var contentType = metadata?.ContentType ?? GuessContentType(extension) ?? "application/octet-stream";
        var downloadFileName = metadata?.OriginalFileName ?? normalizedUid + extension;

        var fileStream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new PhotoFile(fileStream, contentType, downloadFileName);
    }

    /// <inheritdoc />
    public async Task BindToQuotationAsync(string quotationUid, IEnumerable<string> photoUids, CancellationToken cancellationToken)
    {
        var normalizedQuotationUid = NormalizeUid(quotationUid);
        if (normalizedQuotationUid is null)
        {
            throw new QuotationManagementException(HttpStatusCode.BadRequest, "缺少估價單識別碼，無法綁定圖片。");
        }

        var normalizedPhotoUids = NormalizePhotoUids(photoUids);
        if (normalizedPhotoUids.Count == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var photos = await _context.PhotoData
            .Where(photo => normalizedPhotoUids.Contains(photo.PhotoUid))
            .ToListAsync(cancellationToken);

        var existingUids = new HashSet<string>(photos.Select(photo => photo.PhotoUid), StringComparer.OrdinalIgnoreCase);
        var missingUids = normalizedPhotoUids
            .Where(uid => !existingUids.Contains(uid))
            .ToList();

        if (missingUids.Count > 0)
        {
            var missingList = string.Join(", ", missingUids);
            throw new QuotationManagementException(HttpStatusCode.BadRequest, $"找不到以下圖片識別碼：{missingList}，請確認是否已上傳。");
        }

        foreach (var photo in photos)
        {
            photo.QuotationUid = normalizedQuotationUid;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("共綁定 {Count} 張照片至估價單 {QuotationUid}", photos.Count, normalizedQuotationUid);
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 確保儲存根目錄存在，若無則建立。
    /// </summary>
    private string EnsureStorageRoot()
    {
        var root = _storageOptions.RootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "App_Data", "photos");
        }

        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
        }

        return root;
    }

    /// <summary>
    /// 統一產生 PhotoUID，使用 Guid 確保唯一性。
    /// </summary>
    private static string GeneratePhotoUid() => $"PH_{Guid.NewGuid():N}";

    /// <summary>
    /// 解析儲存在資料庫中的照片中繼資料。
    /// </summary>
    private PhotoMetadata? ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PhotoMetadata>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "解析照片中繼資料失敗，原始內容：{Json}", json);
            return null;
        }
    }

    /// <summary>
    /// 若副檔名缺失時依 Content-Type 推測副檔名。
    /// </summary>
    private static string? GuessExtension(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        return contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            _ => null
        };
    }

    /// <summary>
    /// 若 Content-Type 缺失時依副檔名推測。
    /// </summary>
    private string? GuessContentType(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        if (_contentTypeProvider.TryGetContentType("dummy" + extension, out var contentType))
        {
            return contentType;
        }

        return null;
    }

    /// <summary>
    /// 正規化副檔名，確保前面帶有點號且使用小寫。
    /// </summary>
    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        extension = extension.Trim();
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        return extension.ToLowerInvariant();
    }

    /// <summary>
    /// 正規化備註，若為空白則回傳 null。
    /// </summary>
    private static string? NormalizeRemark(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return null;
        }

        return remark.Trim();
    }

    /// <summary>
    /// 正規化 PhotoUID，將空字串視為無效。
    /// </summary>
    private static string? NormalizeUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return null;
        }

        return uid.Trim();
    }

    /// <summary>
    /// 將傳入集合整理為唯一的 PhotoUID 清單。
    /// </summary>
    private static List<string> NormalizePhotoUids(IEnumerable<string> photoUids)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var uid in photoUids)
        {
            var normalizedUid = NormalizeUid(uid);
            if (normalizedUid is not null)
            {
                normalized.Add(normalizedUid);
            }
        }

        return normalized.ToList();
    }

    // ---------- 生命週期 ----------
    // 由 DI 控制生命週期，未持有非受控資源，無需額外釋放。

    /// <summary>
    /// 照片中繼資料，儲存在資料庫的 Comment 欄位。
    /// </summary>
    private class PhotoMetadata
    {
        public string? OriginalFileName { get; set; }
        public string? ContentType { get; set; }
        public string? FileExtension { get; set; }
        public string? Remark { get; set; }
    }
}
