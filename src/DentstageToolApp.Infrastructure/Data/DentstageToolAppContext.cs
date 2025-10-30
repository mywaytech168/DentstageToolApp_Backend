using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DentstageToolApp.Infrastructure.Data;

/// <summary>
/// 系統資料庫內容類別，對應 MySQL 資料表結構與欄位限制。
/// </summary>
public class DentstageToolAppContext : DbContext
{
    private string? _syncLogSourceServer;
    private string? _syncLogStoreType;
    private string? _syncLogServerRole;
    private bool _suppressSyncLogAppend;
    private static string? _defaultSyncLogSourceServer;
    private static string? _defaultSyncLogStoreType;
    private static string? _defaultSyncLogServerRole;
    private static readonly JsonSerializerOptions SyncLogSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> SyncLogExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---------- 排除不需要同步的身分驗證相關資料表 ----------
        "RefreshTokens",
        "DeviceRegistrations",
        "UserAccounts"
    };

    /// <summary>
    /// 建構子，用於注入 DbContext 選項。
    /// </summary>
    public DentstageToolAppContext(DbContextOptions<DentstageToolAppContext> options)
        : base(options)
    {
        // ---------- 初始化同步紀錄預設來源，確保中央環境自動套用 Synced = 1 ----------
        _syncLogSourceServer = _defaultSyncLogSourceServer;
        _syncLogStoreType = _defaultSyncLogStoreType;
        _syncLogServerRole = _defaultSyncLogServerRole;
    }

    /// <summary>
    /// 變更儲存前自動產生同步紀錄，並呼叫同步版 SaveChanges。
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    /// <summary>
    /// 變更儲存前自動產生同步紀錄，於應用程式層取代資料庫 Trigger。
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (!_suppressSyncLogAppend)
        {
            AppendSyncLogs();
        }
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            ClearSyncLogMetadata();
        }
    }

    /// <summary>
    /// 設定同步紀錄的門市與來源資訊，讓呼叫者可於同一交易中補齊欄位。
    /// </summary>
    public void SetSyncLogMetadata(string? sourceServer, string? storeType, string? serverRole = null)
    {
        _syncLogSourceServer = sourceServer;
        _syncLogStoreType = storeType;
        if (!string.IsNullOrWhiteSpace(serverRole))
        {
            _syncLogServerRole = serverRole;
        }
    }

    /// <summary>
    /// 清除暫存的同步紀錄門市資訊，避免影響下一次交易。
    /// </summary>
    public void ClearSyncLogMetadata()
    {
        // ---------- 恢復為預設來源設定，避免跨交易沿用錯誤資訊 ----------
        _syncLogSourceServer = _defaultSyncLogSourceServer;
        _syncLogStoreType = _defaultSyncLogStoreType;
        _syncLogServerRole = _defaultSyncLogServerRole;
    }

    /// <summary>
    /// 停用自動產生同步紀錄的機制，讓中央可直接採用分店傳入的 Sync Log。
    /// </summary>
    public void DisableSyncLogAutoAppend()
    {
        _suppressSyncLogAppend = true;
    }

    /// <summary>
    /// 重新開啟自動產生同步紀錄的機制，恢復預設行為。
    /// </summary>
    public void EnableSyncLogAutoAppend()
    {
        _suppressSyncLogAppend = false;
    }

    /// <summary>
    /// 設定預設的同步紀錄來源，讓中央或門市環境可指定 AppendSyncLogs 的同步旗標與來源別名。
    /// </summary>
    public static void ConfigureSyncLogDefaults(string? sourceServer, string? storeType, string? serverRole)
    {
        // ---------- 儲存預設值給所有 DbContext 實例使用，供中央環境預設標記為已同步 ----------
        _defaultSyncLogSourceServer = sourceServer;
        _defaultSyncLogStoreType = storeType;
        _defaultSyncLogServerRole = serverRole;
    }

    /// <summary>
    /// 顧客資料集。
    /// </summary>
    public virtual DbSet<Customer> Customers => Set<Customer>();

    /// <summary>
    /// 車輛資料集。
    /// </summary>
    public virtual DbSet<Car> Cars => Set<Car>();

    /// <summary>
    /// 車輛品牌資料集。
    /// </summary>
    public virtual DbSet<Brand> Brands => Set<Brand>();

    /// <summary>
    /// 車輛型號資料集。
    /// </summary>
    public virtual DbSet<Model> Models => Set<Model>();

    /// <summary>
    /// 門市資料集。
    /// </summary>
    public virtual DbSet<Store> Stores => Set<Store>();

    /// <summary>
    /// 技師資料集。
    /// </summary>
    public virtual DbSet<Technician> Technicians => Set<Technician>();

    /// <summary>
    /// 報價單資料集。
    /// </summary>
    public virtual DbSet<Quatation> Quatations => Set<Quatation>();

    /// <summary>
    /// 工單資料集。
    /// </summary>
    public virtual DbSet<Order> Orders => Set<Order>();

    /// <summary>
    /// 車體美容資料集。
    /// </summary>
    public virtual DbSet<CarBeauty> CarBeautys => Set<CarBeauty>();

    /// <summary>
    /// 照片資料集。
    /// </summary>
    public virtual DbSet<PhotoDatum> PhotoData => Set<PhotoDatum>();

    /// <summary>
    /// 黑名單資料集。
    /// </summary>
    public virtual DbSet<BlackList> BlackLists => Set<BlackList>();

    /// <summary>
    /// 採購品項類別資料集。
    /// </summary>
    public virtual DbSet<PurchaseCategory> PurchaseCategories => Set<PurchaseCategory>();

    /// <summary>
    /// 採購單資料集。
    /// </summary>
    public virtual DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    /// <summary>
    /// 採購品項資料集。
    /// </summary>
    public virtual DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    /// <summary>
    /// 使用者帳號資料集。
    /// </summary>
    public virtual DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    /// <summary>
    /// 裝置註冊資料集。
    /// </summary>
    public virtual DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();

    /// <summary>
    /// Refresh Token 資料集。
    /// </summary>
    public virtual DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// 同步紀錄資料集。
    /// </summary>
    public virtual DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    /// <summary>
    /// 建立資料模型對應設定。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCustomer(modelBuilder);
        ConfigureBrand(modelBuilder);
        ConfigureModel(modelBuilder);
        ConfigureCar(modelBuilder);
        ConfigureStore(modelBuilder);
        ConfigureTechnician(modelBuilder);
        ConfigureQuatation(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureCarBeauty(modelBuilder);
        ConfigurePhotoData(modelBuilder);
        ConfigureBlackList(modelBuilder);
        ConfigurePurchaseCategory(modelBuilder);
        ConfigurePurchaseOrder(modelBuilder);
        ConfigurePurchaseItem(modelBuilder);
        ConfigureUserAccount(modelBuilder);
        ConfigureDeviceRegistration(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureSyncLog(modelBuilder);
    }

    /// <summary>
    /// 設定顧客資料表欄位與關聯。
    /// </summary>
    private static void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Customer>();
        entity.ToTable("Customers");
        entity.HasKey(e => e.CustomerUid);
        entity.Property(e => e.CustomerUid)
            .HasMaxLength(100)
            .HasColumnName("CustomerUID");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.CustomerType).HasMaxLength(50);
        entity.Property(e => e.Gender).HasMaxLength(10);
        entity.Property(e => e.Connect).HasMaxLength(100);
        entity.Property(e => e.Phone).HasMaxLength(20);
        entity.Property(e => e.PhoneQuery)
            .HasMaxLength(20)
            .HasColumnName("Phone_query");
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.AgeRange).HasMaxLength(50);
        entity.Property(e => e.County).HasMaxLength(50);
        entity.Property(e => e.Township).HasMaxLength(50);
        entity.Property(e => e.Source).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(255);
        entity.Property(e => e.ConnectRemark)
            .HasMaxLength(255)
            .HasColumnName("Connect_Remark");
        entity.Property(e => e.ConnectSameAsName)
            .HasMaxLength(10)
            .HasColumnName("ConnectSameAsName");
    }

    /// <summary>
    /// 設定使用者帳號資料表欄位與關聯。
    /// </summary>
    private static void ConfigureUserAccount(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserAccount>();
        entity.ToTable("UserAccounts");
        entity.HasKey(e => e.UserUid);
        entity.Property(e => e.UserUid)
            .HasMaxLength(100)
            .HasColumnName("UserUID");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.DisplayName).HasMaxLength(100);
        entity.Property(e => e.Role).HasMaxLength(50);
        entity.Property(e => e.ServerRole).HasMaxLength(50);
        entity.Property(e => e.ServerIp).HasMaxLength(100);
        entity.Property(e => e.LastUploadTime)
            .HasColumnType("datetime");
        entity.Property(e => e.LastDownloadTime)
            .HasColumnType("datetime");
        entity.Property(e => e.LastSyncCount);
        entity.HasMany(e => e.DeviceRegistrations)
            .WithOne(e => e.UserAccount)
            .HasForeignKey(e => e.UserUid)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(e => e.RefreshTokens)
            .WithOne(e => e.UserAccount)
            .HasForeignKey(e => e.UserUid)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// 設定裝置註冊資料表欄位與關聯。
    /// </summary>
    private static void ConfigureDeviceRegistration(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DeviceRegistration>();
        entity.ToTable("DeviceRegistrations");
        entity.HasKey(e => e.DeviceRegistrationUid);
        entity.Property(e => e.DeviceRegistrationUid)
            .HasMaxLength(100)
            .HasColumnName("DeviceRegistrationUID");
        entity.Property(e => e.UserUid)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("UserUID");
        entity.Property(e => e.DeviceKey)
            .IsRequired()
            .HasMaxLength(150);
        entity.Property(e => e.DeviceName).HasMaxLength(100);
        entity.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.HasIndex(e => new { e.UserUid, e.DeviceKey })
            .IsUnique();
        entity.HasMany(e => e.RefreshTokens)
            .WithOne(e => e.DeviceRegistration)
            .HasForeignKey(e => e.DeviceRegistrationUid)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// 設定 Refresh Token 資料表欄位與關聯。
    /// </summary>
    private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RefreshToken>();
        entity.ToTable("RefreshTokens");
        entity.HasKey(e => e.RefreshTokenUid);
        entity.Property(e => e.RefreshTokenUid)
            .HasMaxLength(100)
            .HasColumnName("RefreshTokenUID");
        entity.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(500);
        entity.Property(e => e.UserUid)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("UserUID");
        entity.Property(e => e.DeviceRegistrationUid)
            .HasMaxLength(100)
            .HasColumnName("DeviceRegistrationUID");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.HasIndex(e => e.Token)
            .IsUnique();
    }

    /// <summary>
    /// 設定車輛資料表欄位與關聯。
    /// </summary>
    private static void ConfigureCar(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Car>();
        entity.ToTable("Cars");
        entity.HasKey(e => e.CarUid);
        entity.Property(e => e.CarUid)
            .HasMaxLength(100)
            .HasColumnName("CarUID");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.CarNo).HasMaxLength(50);
        entity.Property(e => e.CarNoQuery)
            .HasMaxLength(50)
            .HasColumnName("CarNo_query");
        entity.Property(e => e.Brand).HasMaxLength(50);
        entity.Property(e => e.Model).HasMaxLength(50);
        entity.Property(e => e.Color).HasMaxLength(50);
        entity.Property(e => e.CarRemark)
            .HasMaxLength(255)
            .HasColumnName("Car_Remark");
        entity.Property(e => e.Milage)
            .HasColumnName("Milage");
        entity.Property(e => e.BrandModel)
            .HasMaxLength(100)
            .HasColumnName("Brand_Model");
    }

    /// <summary>
    /// 設定車輛品牌資料表欄位與關聯。
    /// </summary>
    private static void ConfigureBrand(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Brand>();
        entity.ToTable("Brands");
        entity.HasKey(e => e.BrandUid);
        entity.Property(e => e.BrandUid)
            .HasMaxLength(100)
            .HasColumnName("BrandUID");
        entity.Property(e => e.BrandName)
            .IsRequired()
            .HasMaxLength(100);
    }

    /// <summary>
    /// 設定車輛型號資料表欄位與關聯。
    /// </summary>
    private static void ConfigureModel(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Model>();
        entity.ToTable("Models");
        entity.HasKey(e => e.ModelUid);
        entity.Property(e => e.ModelUid)
            .HasMaxLength(100)
            .HasColumnName("ModelUID");
        entity.Property(e => e.ModelName)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.BrandUid)
            .HasMaxLength(100)
            .HasColumnName("BrandUID");
        entity.HasOne(e => e.Brand)
            .WithMany(e => e.Models)
            .HasForeignKey(e => e.BrandUid)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// 設定門市主檔欄位與關聯。
    /// </summary>
    private static void ConfigureStore(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Store>();
        entity.ToTable("stores");
        entity.HasKey(e => e.StoreUid);
        entity.Property(e => e.StoreUid)
            .HasMaxLength(100)
            .HasColumnName("StoreUID");
        entity.Property(e => e.StoreName)
            .IsRequired()
            .HasMaxLength(100);
        entity.HasMany(e => e.Technicians)
            .WithOne(e => e.Store)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(e => e.Quatations)
            .WithOne(e => e.StoreNavigation)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(e => e.PurchaseOrders)
            .WithOne(e => e.Store)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// 設定技師主檔欄位與關聯。
    /// </summary>
    private static void ConfigureTechnician(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Technician>();
        entity.ToTable("technicians");
        entity.HasKey(e => e.TechnicianUid);
        entity.Property(e => e.TechnicianUid)
            .HasMaxLength(100)
            .HasColumnName("TechnicianUID");
        entity.Property(e => e.TechnicianName)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.JobTitle)
            .HasMaxLength(50)
            .HasColumnName("JobTitle");
        entity.Property(e => e.StoreUid)
            .HasMaxLength(100)
            .HasColumnName("StoreUID");
        entity.HasOne(e => e.Store)
            .WithMany(e => e.Technicians)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(e => e.Quatations)
            .WithOne(e => e.EstimationTechnicianNavigation)
            .HasForeignKey(e => e.EstimationTechnicianUid)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// 設定報價單資料表欄位與關聯。
    /// </summary>
    private static void ConfigureQuatation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Quatation>();
        entity.ToTable("Quatations");
        entity.HasKey(e => e.QuotationUid);
        entity.Property(e => e.QuotationUid)
            .HasMaxLength(100)
            .HasColumnName("QuotationUID");
        entity.Property(e => e.QuotationNo).HasMaxLength(50);
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.StoreUid)
            .HasMaxLength(100)
            .HasColumnName("StoreUID");
        entity.Property(e => e.UserUid).HasMaxLength(100);
        entity.Property(e => e.UserName).HasMaxLength(100);
        entity.Property(e => e.EstimationTechnicianUid)
            .HasMaxLength(100)
            .HasColumnName("EstimationTechnicianUID");
        entity.Property(e => e.CreatorTechnicianUid)
            .HasMaxLength(100)
            .HasColumnName("CreatorTechnicianUID");
        entity.Property(e => e.Status).HasMaxLength(20);
        entity.Property(e => e.FixType)
            .HasMaxLength(100)
            .HasColumnName("Fix_Type");
        entity.Property(e => e.CarUid)
            .HasMaxLength(100)
            .HasColumnName("CarUID");
        entity.Property(e => e.CarNoInputGlobal)
            .HasMaxLength(50)
            .HasColumnName("CarNo_input_Global");
        entity.Property(e => e.CarNoInput)
            .HasMaxLength(50)
            .HasColumnName("CarNo_input");
        entity.Property(e => e.CarNo).HasMaxLength(50);
        entity.Property(e => e.Brand).HasMaxLength(50);
        entity.Property(e => e.Model).HasMaxLength(50);
        entity.Property(e => e.BrandUid)
            .HasMaxLength(100)
            .HasColumnName("BrandUID");
        entity.Property(e => e.ModelUid)
            .HasMaxLength(100)
            .HasColumnName("ModelUID");
        entity.Property(e => e.Color).HasMaxLength(20);
        entity.Property(e => e.CarRemark)
            .HasMaxLength(255)
            .HasColumnName("Car_Remark");
        entity.Property(e => e.Milage)
            .HasColumnName("Milage");
        entity.Property(e => e.BrandModel)
            .HasMaxLength(100)
            .HasColumnName("Brand_Model");
        entity.HasOne(e => e.BrandNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.BrandUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.ModelNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.ModelUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.StoreNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.EstimationTechnicianNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.EstimationTechnicianUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.Property(e => e.CustomerUid)
            .HasMaxLength(100)
            .HasColumnName("CustomerUID");
        entity.Property(e => e.PhoneInputGlobal)
            .HasMaxLength(20)
            .HasColumnName("Phone_input_Global");
        entity.Property(e => e.PhoneInput)
            .HasMaxLength(20)
            .HasColumnName("Phone_input");
        entity.Property(e => e.Phone).HasMaxLength(20);
        entity.Property(e => e.CustomerType)
            .HasMaxLength(50)
            .HasColumnName("Customer_type");
        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.Gender).HasMaxLength(10);
        entity.Property(e => e.Connect).HasMaxLength(100);
        entity.Property(e => e.County).HasMaxLength(50);
        entity.Property(e => e.Township).HasMaxLength(50);
        entity.Property(e => e.Source).HasMaxLength(100);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(255);
        entity.Property(e => e.ConnectRemark)
            .HasMaxLength(255)
            .HasColumnName("Connect_Remark");
        entity.Property(e => e.Valuation)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.DiscountPercent)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("Discount_percent");
        entity.Property(e => e.Discount)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.DiscountReason)
            .HasMaxLength(255)
            .HasColumnName("Discount_reason");
        entity.Property(e => e.DentOtherFee)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("DentOtherFee");
        entity.Property(e => e.DentPercentageDiscount)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("DentPercentageDiscount");
        entity.Property(e => e.DentDiscountReason)
            .HasMaxLength(255)
            .HasColumnName("DentDiscountReason");
        entity.Property(e => e.PaintOtherFee)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("PaintOtherFee");
        entity.Property(e => e.PaintPercentageDiscount)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("PaintPercentageDiscount");
        entity.Property(e => e.PaintDiscountReason)
            .HasMaxLength(255)
            .HasColumnName("PaintDiscountReason");
        entity.Property(e => e.OtherOtherFee)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("OtherOtherFee");
        entity.Property(e => e.OtherPercentageDiscount)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("OtherPercentageDiscount");
        entity.Property(e => e.OtherDiscountReason)
            .HasMaxLength(255)
            .HasColumnName("OtherDiscountReason");
        entity.Property(e => e.BookDate)
            .HasMaxLength(20)
            .HasColumnName("Book_Date");
        entity.Property(e => e.BookMethod)
            .HasMaxLength(50)
            .HasColumnName("Book_method");
        entity.Property(e => e.CarReserved)
            .HasMaxLength(50)
            .HasColumnName("CarReserved");
        entity.Property(e => e.FixDate)
            .HasColumnName("Fix_Date");
        entity.Property(e => e.ToolTest)
            .HasMaxLength(50);
        entity.Property(e => e.Coat)
            .HasMaxLength(50);
        entity.Property(e => e.Envelope)
            .HasMaxLength(50);
        entity.Property(e => e.Paint)
            .HasMaxLength(50);
        entity.Property(e => e.Remark)
            .HasColumnType("text");
        entity.Property(e => e.Status110Timestamp)
            .HasColumnName("Status110_TimeStamp");
        entity.Property(e => e.Status110User)
            .HasMaxLength(50)
            .HasColumnName("Status110_User");
        entity.Property(e => e.Status180Timestamp)
            .HasColumnName("Status180_TimeStamp");
        entity.Property(e => e.Status180User)
            .HasMaxLength(50)
            .HasColumnName("Status180_User");
        entity.Property(e => e.Status190Timestamp)
            .HasColumnName("Status190_TimeStamp");
        entity.Property(e => e.Status190User)
            .HasMaxLength(50)
            .HasColumnName("Status190_User");
        entity.Property(e => e.Status191Timestamp)
            .HasColumnName("Status191_TimeStamp");
        entity.Property(e => e.Status191User)
            .HasMaxLength(50)
            .HasColumnName("Status191_User");
        entity.Property(e => e.Status199Timestamp)
            .HasColumnName("Status199_TimeStamp");
        entity.Property(e => e.Status199User)
            .HasMaxLength(50)
            .HasColumnName("Status199_User");
        entity.Property(e => e.CurrentStatusDate)
            .HasColumnName("CurrentStatus_Date");
        entity.Property(e => e.CurrentStatusUser)
            .HasMaxLength(50)
            .HasColumnName("CurrentStatus_User");
        entity.Property(e => e.FixExpect)
            .HasMaxLength(50);
        entity.Property(e => e.Reject)
            .HasColumnType("tinyint(1)");
        entity.Property(e => e.RejectReason)
            .HasMaxLength(255)
            .HasColumnName("Reject_reason");
        entity.Property(e => e.PanelBeat)
            .HasMaxLength(50);
        entity.Property(e => e.PanelBeatReason)
            .HasMaxLength(255)
            .HasColumnName("PanelBeat_reason");
        entity.Property(e => e.FixTimeHour)
            .HasColumnName("Fix_Time_Hour");
        entity.Property(e => e.FixTimeMin)
            .HasColumnName("Fix_Time_Min");
        entity.Property(e => e.FixExpectDay)
            .HasColumnName("FixExpect_Day");
        entity.Property(e => e.FixExpectHour)
            .HasColumnName("FixExpect_Hour");
        entity.Property(e => e.FlagRegularCustomer)
            .HasColumnType("tinyint(1)")
            .HasColumnName("Flag_RegularCustomer");

        entity.HasOne(d => d.Customer)
            .WithMany(p => p.Quatations)
            .HasForeignKey(d => d.CustomerUid)
            .HasConstraintName("FK_Quatations_Customers");

        entity.HasOne(d => d.Car)
            .WithMany(p => p.Quatations)
            .HasForeignKey(d => d.CarUid)
            .HasConstraintName("FK_Quatations_Cars");
    }

    /// <summary>
    /// 設定工單資料表欄位與關聯。
    /// </summary>
    private static void ConfigureOrder(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Order>();
        entity.ToTable("Orders");
        entity.HasKey(e => e.OrderUid);
        entity.Property(e => e.OrderUid)
            .HasMaxLength(100)
            .HasColumnName("OrderUID");
        entity.Property(e => e.OrderNo).HasMaxLength(50);
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.StoreUid).HasMaxLength(100);
        entity.Property(e => e.UserUid).HasMaxLength(100);
        entity.Property(e => e.UserName).HasMaxLength(100);
        entity.Property(e => e.EstimationTechnicianUid)
            .HasMaxLength(100)
            .HasColumnName("EstimationTechnicianUID");
        entity.Property(e => e.CreatorTechnicianUid)
            .HasMaxLength(100)
            .HasColumnName("CreatorTechnicianUID");
        entity.Property(e => e.Status).HasMaxLength(20);
        entity.Property(e => e.CarUid)
            .HasMaxLength(100)
            .HasColumnName("CarUID");
        entity.Property(e => e.CarNoInputGlobal)
            .HasMaxLength(50)
            .HasColumnName("CarNo_input_Global");
        entity.Property(e => e.CarNoInput)
            .HasMaxLength(50)
            .HasColumnName("CarNo_input");
        entity.Property(e => e.CarNo).HasMaxLength(50);
        entity.Property(e => e.Brand).HasMaxLength(50);
        entity.Property(e => e.Model).HasMaxLength(50);
        entity.Property(e => e.Color).HasMaxLength(50);
        entity.Property(e => e.CarRemark)
            .HasMaxLength(255)
            .HasColumnName("Car_Remark");
        entity.Property(e => e.Milage)
            .HasColumnName("Milage");
        entity.Property(e => e.BrandModel)
            .HasMaxLength(100)
            .HasColumnName("Brand_Model");
        entity.Property(e => e.CustomerUid)
            .HasMaxLength(100)
            .HasColumnName("CustomerUID");
        entity.Property(e => e.CustomerType)
            .HasMaxLength(50)
            .HasColumnName("Customer_type");
        entity.Property(e => e.PhoneInputGlobal)
            .HasMaxLength(20)
            .HasColumnName("Phone_input_Global");
        entity.Property(e => e.PhoneInput)
            .HasMaxLength(20)
            .HasColumnName("Phone_input");
        entity.Property(e => e.Phone).HasMaxLength(20);
        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.Gender).HasMaxLength(10);
        entity.Property(e => e.Connect).HasMaxLength(100);
        entity.Property(e => e.County).HasMaxLength(50);
        entity.Property(e => e.Township).HasMaxLength(50);
        entity.Property(e => e.Source).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(255);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.ConnectRemark)
            .HasMaxLength(255)
            .HasColumnName("Connect_Remark");
        entity.Property(e => e.QuatationUid)
            .HasMaxLength(100)
            .HasColumnName("QuatationUID");
        entity.Property(e => e.BookDate)
            .HasMaxLength(20)
            .HasColumnName("Book_Date");
        entity.Property(e => e.BookMethod)
            .HasMaxLength(50)
            .HasColumnName("Book_method");
        entity.Property(e => e.WorkDate)
            .HasMaxLength(20)
            .HasColumnName("Work_Date");
        entity.Property(e => e.WorkDateRemark)
            .HasMaxLength(255)
            .HasColumnName("Work_Date_remark");
        entity.Property(e => e.FixType)
            .HasMaxLength(100)
            .HasColumnName("Fix_Type");
        entity.Property(e => e.Content).HasColumnType("text");
        entity.Property(e => e.CarReserved).HasMaxLength(50);
        entity.Property(e => e.Remark).HasColumnType("text");
        entity.Property(e => e.Status210Date)
            .HasColumnName("Status210_Date");
        entity.Property(e => e.Status210User)
            .HasMaxLength(50)
            .HasColumnName("Status210_User");
        entity.Property(e => e.Status220Date)
            .HasColumnName("Status220_Date");
        entity.Property(e => e.Status220User)
            .HasMaxLength(50)
            .HasColumnName("Status220_User");
        entity.Property(e => e.Status290Date)
            .HasColumnName("Status290_Date");
        entity.Property(e => e.Status290User)
            .HasMaxLength(50)
            .HasColumnName("Status290_User");
        entity.Property(e => e.Status295Timestamp)
            .HasColumnName("Status295_Timestamp");
        entity.Property(e => e.Status295User)
            .HasMaxLength(50)
            .HasColumnName("Status295_User");
        entity.Property(e => e.WorkRecordUid)
            .HasMaxLength(100)
            .HasColumnName("g_WorkRecordUID");
        entity.Property(e => e.CurrentStatusDate)
            .HasColumnName("CurrentStatus_Date");
        entity.Property(e => e.CurrentStatusUser)
            .HasMaxLength(50)
            .HasColumnName("CurrentStatus_User");
        entity.Property(e => e.SignatureModifyTimestamp)
            .HasColumnName("Signature_ModifyTimeStamp");
        entity.Property(e => e.Valuation)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.DiscountPercent)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("Discount_percent");
        entity.Property(e => e.Discount)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.DiscountReason)
            .HasMaxLength(255)
            .HasColumnName("Discount_reason");
        entity.Property(e => e.DentOtherFee)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("DentOtherFee");
        entity.Property(e => e.DentPercentageDiscount)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("DentPercentageDiscount");
        entity.Property(e => e.DentDiscountReason)
            .HasMaxLength(255)
            .HasColumnName("DentDiscountReason");
        entity.Property(e => e.PaintOtherFee)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("PaintOtherFee");
        entity.Property(e => e.PaintPercentageDiscount)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("PaintPercentageDiscount");
        entity.Property(e => e.PaintDiscountReason)
            .HasMaxLength(255)
            .HasColumnName("PaintDiscountReason");
        entity.Property(e => e.OtherOtherFee)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("OtherOtherFee");
        entity.Property(e => e.OtherPercentageDiscount)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("OtherPercentageDiscount");
        entity.Property(e => e.OtherDiscountReason)
            .HasMaxLength(255)
            .HasColumnName("OtherDiscountReason");
        entity.Property(e => e.Amount)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.StopReason)
            .HasMaxLength(255)
            .HasColumnName("Stop_Reason");
        entity.Property(e => e.Rebate)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.FlagRegularCustomer)
            .HasColumnType("tinyint(1)")
            .HasColumnName("Flag_RegularCustomer");
        entity.Property(e => e.FlagExternalCooperation)
            .HasColumnType("tinyint(1)")
            .HasColumnName("Flag_ExternalCooperation");

        entity.HasOne(d => d.Quatation)
            .WithMany(p => p.Orders)
            .HasForeignKey(d => d.QuatationUid)
            .HasConstraintName("FK_Orders_Quatations");

        entity.HasOne(d => d.Customer)
            .WithMany(p => p.Orders)
            .HasForeignKey(d => d.CustomerUid)
            .HasConstraintName("FK_Orders_Customers");

        entity.HasOne(d => d.Car)
            .WithMany(p => p.Orders)
            .HasForeignKey(d => d.CarUid)
            .HasConstraintName("FK_Orders_Cars");
    }

    /// <summary>
    /// 設定車體美容資料表欄位與關聯。
    /// </summary>
    private static void ConfigureCarBeauty(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CarBeauty>();
        entity.ToTable("CarBeautys");
        entity.HasKey(e => e.QuotationUid);
        entity.Property(e => e.QuotationUid)
            .HasMaxLength(100)
            .HasColumnName("QuotationUID");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.Service1).HasMaxLength(10);
        entity.Property(e => e.Service1Sub1)
            .HasMaxLength(10)
            .HasColumnName("Service1_Sub1");
        entity.Property(e => e.Service1Sub2)
            .HasMaxLength(10)
            .HasColumnName("Service1_Sub2");
        entity.Property(e => e.Service1Sub2Value)
            .HasMaxLength(50)
            .HasColumnName("Service1_Sub2_Value");
        entity.Property(e => e.Service1Show)
            .HasMaxLength(255)
            .HasColumnName("Service1_Show");
        entity.Property(e => e.Service2).HasMaxLength(10);
        entity.Property(e => e.Service2Value)
            .HasMaxLength(50)
            .HasColumnName("Service2_Value");
        entity.Property(e => e.Service2ValueRemark)
            .HasMaxLength(255)
            .HasColumnName("Service2_Value_Remark");
        entity.Property(e => e.Service2Show)
            .HasMaxLength(255)
            .HasColumnName("Service2_Show");
        entity.Property(e => e.Service3).HasMaxLength(10);
        entity.Property(e => e.Service3Value)
            .HasMaxLength(50)
            .HasColumnName("Service3_Value");
        entity.Property(e => e.Service3Show)
            .HasMaxLength(255)
            .HasColumnName("Service3_Show");
        entity.Property(e => e.Service4).HasMaxLength(10);
        entity.Property(e => e.Service4Value1)
            .HasMaxLength(50)
            .HasColumnName("Service4_Value1");
        entity.Property(e => e.Service4Value1Remark)
            .HasMaxLength(255)
            .HasColumnName("Service4_Value1_Remark");
        entity.Property(e => e.Service4Value2)
            .HasMaxLength(50)
            .HasColumnName("Service4_Value2");
        entity.Property(e => e.Service4Value2Remark)
            .HasMaxLength(255)
            .HasColumnName("Service4_Value2_Remark");
        entity.Property(e => e.Service4Show)
            .HasMaxLength(255)
            .HasColumnName("Service4_Show");
        entity.Property(e => e.Service5).HasMaxLength(10);
        entity.Property(e => e.Service5Value)
            .HasMaxLength(50)
            .HasColumnName("Service5_Value");
        entity.Property(e => e.Service5ValueRemark)
            .HasMaxLength(255)
            .HasColumnName("Service5_Value_Remark");
        entity.Property(e => e.Service5Show)
            .HasMaxLength(255)
            .HasColumnName("Service5_Show");
        entity.Property(e => e.Remark).HasColumnType("text");
        entity.Property(e => e.ServiceShow)
            .HasColumnType("text")
            .HasColumnName("Service_Show");
        entity.Property(e => e.ServiceCode)
            .HasMaxLength(10)
            .HasColumnName("Service_Code");

        entity.HasOne(d => d.Quatation)
            .WithOne(p => p.CarBeauty)
            .HasForeignKey<CarBeauty>(d => d.QuotationUid)
            .HasConstraintName("FK_CarBeautys_Quatations");
    }

    /// <summary>
    /// 設定照片資料表欄位與關聯。
    /// </summary>
    private static void ConfigurePhotoData(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PhotoDatum>();
        entity.ToTable("PhotoData");
        entity.HasKey(e => e.PhotoUid);
        entity.Property(e => e.PhotoUid)
            .HasMaxLength(100)
            .HasColumnName("PhotoUID");
        entity.Property(e => e.QuotationUid)
            .HasMaxLength(100)
            .HasColumnName("QuotationUID");
        entity.Property(e => e.RelatedUid)
            .HasMaxLength(100)
            .HasColumnName("RelatedUID");
        entity.Property(e => e.Posion).HasMaxLength(50);
        entity.Property(e => e.Comment).HasColumnType("text");
        entity.Property(e => e.PhotoShape)
            .HasMaxLength(50)
            .HasColumnName("Photo_shape");
        entity.Property(e => e.PhotoShapeOther)
            .HasMaxLength(50)
            .HasColumnName("Photo_shape_other");
        entity.Property(e => e.PhotoShapeShow)
            .HasMaxLength(50)
            .HasColumnName("Photo_shape_show");
        entity.Property(e => e.Cost)
            .HasColumnType("decimal(10,2)");
        entity.Property(e => e.FlagFinish)
            .HasColumnType("tinyint(1)")
            .HasColumnName("Flag_Finish");
        entity.Property(e => e.FinishCost)
            .HasColumnType("decimal(10,2)")
            .HasColumnName("Finish_Cost");
        entity.Property(e => e.MaintenanceProgress)
            .HasColumnType("decimal(5,2)")
            .HasColumnName("MaintenanceProgress");
        entity.Property(e => e.Stage)
            .HasMaxLength(20)
            .HasColumnName("PhotoStage");
        entity.Property(e => e.FixType)
            .HasMaxLength(100)
            .HasColumnName("Fix_Type");

        entity.HasOne(d => d.Quatation)
            .WithMany(p => p.PhotoData)
            .HasForeignKey(d => d.QuotationUid)
            .HasConstraintName("FK_PhotoData_Quatations");
    }

    /// <summary>
    /// 設定黑名單資料表欄位與關聯。
    /// </summary>
    private static void ConfigureBlackList(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BlackList>();
        entity.ToTable("BlackLists");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("ID");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.StoreUid).HasMaxLength(100);
        entity.Property(e => e.UserUid).HasMaxLength(100);
        entity.Property(e => e.Status).HasMaxLength(50);
        entity.Property(e => e.CancelDate)
            .HasMaxLength(20);
        entity.Property(e => e.BookDate)
            .HasMaxLength(20);
        entity.Property(e => e.FixDate)
            .HasMaxLength(20);
        entity.Property(e => e.CustomerUid)
            .HasMaxLength(100)
            .HasColumnName("CustomerUID");
        entity.Property(e => e.CustomerName)
            .HasMaxLength(100);
        entity.Property(e => e.CustomerPhone)
            .HasMaxLength(20);
        entity.Property(e => e.CustomerPhoneFilter)
            .HasMaxLength(20)
            .HasColumnName("CustomerPhone_filter");
        entity.Property(e => e.FlagBlack)
            .HasColumnType("tinyint(1)")
            .HasColumnName("Flag_Black");
        entity.Property(e => e.Reason)
            .HasMaxLength(255);
        entity.Property(e => e.QuotationUid)
            .HasMaxLength(100)
            .HasColumnName("QuotationUID");
        entity.Property(e => e.UserName)
            .HasMaxLength(100);

        entity.HasOne(d => d.Customer)
            .WithMany(p => p.BlackLists)
            .HasForeignKey(d => d.CustomerUid)
            .HasConstraintName("FK_BlackLists_Customers");
    }

    /// <summary>
    /// 設定採購品項類別資料表欄位與關聯。
    /// </summary>
    private static void ConfigurePurchaseCategory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseCategory>();
        entity.ToTable("PurchaseCategories");
        entity.HasKey(e => e.CategoryUid);
        entity.Property(e => e.CategoryUid)
            .HasMaxLength(100)
            .HasColumnName("CategoryUID");
        entity.Property(e => e.CategoryName)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.CreationTimestamp)
            .HasColumnType("datetime");
        entity.Property(e => e.ModificationTimestamp)
            .HasColumnType("datetime");
    }

    /// <summary>
    /// 設定採購單資料表欄位與關聯。
    /// </summary>
    private static void ConfigurePurchaseOrder(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseOrder>();
        entity.ToTable("PurchaseOrders");
        entity.HasKey(e => e.PurchaseOrderUid);
        entity.Property(e => e.PurchaseOrderUid)
            .HasMaxLength(100)
            .HasColumnName("PurchaseOrderUID");
        entity.Property(e => e.PurchaseOrderNo)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.StoreUid)
            .HasMaxLength(100)
            .HasColumnName("StoreUID");
        entity.HasOne(e => e.Store)
            .WithMany(e => e.PurchaseOrders)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.Property(e => e.PurchaseDate)
            .HasColumnType("date");
        entity.Property(e => e.TotalAmount)
            .HasColumnType("decimal(18,2)");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.CreationTimestamp)
            .HasColumnType("datetime");
        entity.Property(e => e.ModificationTimestamp)
            .HasColumnType("datetime");
    }

    /// <summary>
    /// 設定採購品項資料表欄位與關聯。
    /// </summary>
    private static void ConfigurePurchaseItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseItem>();
        entity.ToTable("PurchaseItems");
        entity.HasKey(e => e.PurchaseItemUid);
        entity.Property(e => e.PurchaseItemUid)
            .HasMaxLength(100)
            .HasColumnName("PurchaseItemUID");
        entity.Property(e => e.PurchaseOrderUid)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("PurchaseOrderUID");
        entity.Property(e => e.ItemName)
            .IsRequired()
            .HasMaxLength(200);
        entity.Property(e => e.CategoryUid)
            .HasMaxLength(100)
            .HasColumnName("CategoryUID");
        entity.Property(e => e.UnitPrice)
            .HasColumnType("decimal(18,2)");
        entity.Property(e => e.TotalAmount)
            .HasColumnType("decimal(18,2)");
        entity.Property(e => e.CreatedBy).HasMaxLength(50);
        entity.Property(e => e.ModifiedBy).HasMaxLength(50);
        entity.Property(e => e.CreationTimestamp)
            .HasColumnType("datetime");
        entity.Property(e => e.ModificationTimestamp)
            .HasColumnType("datetime");

        entity.HasOne(e => e.PurchaseOrder)
            .WithMany(order => order.PurchaseItems)
            .HasForeignKey(e => e.PurchaseOrderUid)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Category)
            .WithMany(category => category.PurchaseItems)
            .HasForeignKey(e => e.CategoryUid)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// 設定同步紀錄資料表欄位與索引。
    /// </summary>
    private static void ConfigureSyncLog(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SyncLog>();
        entity.ToTable("SyncLogs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id)
            .IsRequired()
            .HasColumnType("char(36)")
            .ValueGeneratedNever();
        entity.Property(e => e.TableName)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.RecordId)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(20);
        entity.Property(e => e.SourceServer)
            .HasMaxLength(50);
        entity.Property(e => e.StoreType)
            .HasMaxLength(50);
        entity.Property(e => e.Payload)
            .HasColumnType("longtext");
        entity.Property(e => e.UpdatedAt)
            .HasColumnType("datetime");
        entity.Property(e => e.SyncedAt)
            .HasColumnType("datetime");
        entity.HasIndex(e => new { e.TableName, e.StoreType, e.SyncedAt });
        entity.HasIndex(e => e.Synced);
    }

    /// <summary>
    /// 將目前追蹤的資料異動轉換為同步紀錄，由應用程式層統一插入 SyncLogs。
    /// </summary>
    private void AppendSyncLogs()
    {
        // ---------- 只針對新增、更新、刪除的實體產生同步紀錄 ----------
        var trackedEntries = ChangeTracker
            .Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Entity is not SyncLog)
            .ToList();

        if (trackedEntries.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var logs = new List<SyncLog>();

        foreach (var entry in trackedEntries)
        {
            // ---------- 若無實質異動則略過，避免產生多餘紀錄 ----------
            if (entry.State == EntityState.Modified && !entry.Properties.Any(property => property.IsModified))
            {
                continue;
            }

            var tableName = entry.Metadata.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = entry.Entity.GetType().Name;
            }

            if (SyncLogExcludedTables.Contains(tableName))
            {
                // ---------- 排除不應同步的敏感或驗證資料表 ----------
                continue;
            }

            var keyValues = entry.Properties
                .Where(property => property.Metadata.IsPrimaryKey())
                .Select(property =>
                {
                    var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
                    return value?.ToString() ?? string.Empty;
                })
                .ToArray();

            if (keyValues.Length == 0)
            {
                continue;
            }

            var recordId = string.Join(",", keyValues);
            var action = entry.State switch
            {
                EntityState.Added => "INSERT",
                EntityState.Deleted => "DELETE",
                _ => "UPDATE"
            };

            string? payloadJson = null;
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                // ---------- 針對新增與更新行為保存最新欄位快照，便於後續同步還原異動 ----------
                var snapshot = BuildPropertySnapshot(entry, useOriginalValues: false);
                if (snapshot.Count > 0)
                {
                    payloadJson = JsonSerializer.Serialize(snapshot, SyncLogSerializerOptions);
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                // ---------- 刪除行為保留原始欄位資料，有助於除錯與需要時重建 ----------
                var snapshot = BuildPropertySnapshot(entry, useOriginalValues: true);
                if (snapshot.Count > 0)
                {
                    payloadJson = JsonSerializer.Serialize(snapshot, SyncLogSerializerOptions);
                }
            }

            // ---------- 依伺服器角色決定同步旗標，中央派發的資料須直接標記為已同步 ----------
            var shouldMarkSynced = string.Equals(_syncLogServerRole, "中央", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_syncLogServerRole, "Central", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_syncLogServerRole, "CentralServer", StringComparison.OrdinalIgnoreCase);

            logs.Add(new SyncLog
            {
                Id = Guid.NewGuid(),
                TableName = tableName,
                RecordId = recordId,
                Action = action,
                // ---------- 伺服器產生的同步紀錄更新時間即為當下時間 ----------
                UpdatedAt = now,
                // ---------- 同步時間同樣記錄為產生時刻，供門市端以此判斷差異 ----------
                SyncedAt = now,
                SourceServer = _syncLogSourceServer,
                StoreType = _syncLogStoreType,
                Synced = shouldMarkSynced,
                Payload = payloadJson
            });
        }

        if (logs.Count > 0)
        {
            // ---------- 集中新增同步紀錄，避免於迴圈中觸發追蹤狀態變化 ----------
            SyncLogs.AddRange(logs);
        }
    }

    /// <summary>
    /// 建立同步紀錄所需的欄位快照，支援原始或目前數值。
    /// </summary>
    private static Dictionary<string, object?> BuildPropertySnapshot(EntityEntry entry, bool useOriginalValues)
    {
        var snapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in entry.Properties)
        {
            // ---------- 以欄位名稱為鍵保存數值，保留 null 供 JSON 序列化使用 ----------
            var value = useOriginalValues ? property.OriginalValue : property.CurrentValue;
            snapshot[property.Metadata.Name] = value;
        }

        return snapshot;
    }
}
