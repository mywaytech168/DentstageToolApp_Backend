using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentstageToolApp.Infrastructure.Data;

/// <summary>
/// 系統資料庫內容類別，對應 MySQL 資料表結構與欄位限制。
/// </summary>
public class DentstageToolAppContext : DbContext
{
    /// <summary>
    /// 建構子，用於注入 DbContext 選項。
    /// </summary>
    public DentstageToolAppContext(DbContextOptions<DentstageToolAppContext> options)
        : base(options)
    {
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
    /// 維修類型資料集。
    /// </summary>
    public virtual DbSet<FixType> FixTypes => Set<FixType>();

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
    /// 門市同步狀態資料集。
    /// </summary>
    public virtual DbSet<StoreSyncState> StoreSyncStates => Set<StoreSyncState>();

    /// <summary>
    /// 建立資料模型對應設定。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCustomer(modelBuilder);
        ConfigureBrand(modelBuilder);
        ConfigureModel(modelBuilder);
        ConfigureCar(modelBuilder);
        ConfigureFixType(modelBuilder);
        ConfigureStore(modelBuilder);
        ConfigureTechnician(modelBuilder);
        ConfigureQuatation(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureCarBeauty(modelBuilder);
        ConfigurePhotoData(modelBuilder);
        ConfigureBlackList(modelBuilder);
        ConfigureUserAccount(modelBuilder);
        ConfigureDeviceRegistration(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureSyncLog(modelBuilder);
        ConfigureStoreSyncState(modelBuilder);
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
    /// 設定維修類型主檔欄位與關聯。
    /// </summary>
    private static void ConfigureFixType(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FixType>();
        entity.ToTable("fix_types");
        entity.HasKey(e => e.FixTypeUid);
        entity.Property(e => e.FixTypeUid)
            .HasMaxLength(100)
            .HasColumnName("FixTypeUID");
        entity.Property(e => e.FixTypeName)
            .IsRequired()
            .HasMaxLength(100);
        entity.HasMany(e => e.Quatations)
            .WithOne(e => e.FixTypeNavigation)
            .HasForeignKey(e => e.FixTypeUid)
            .OnDelete(DeleteBehavior.SetNull);
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
        entity.Property(e => e.StoreUid)
            .HasMaxLength(100)
            .HasColumnName("StoreUID");
        entity.HasOne(e => e.Store)
            .WithMany(e => e.Technicians)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(e => e.Quatations)
            .WithOne(e => e.TechnicianNavigation)
            .HasForeignKey(e => e.TechnicianUid)
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
        entity.Property(e => e.TechnicianUid)
            .HasMaxLength(100)
            .HasColumnName("TechnicianUID");
        entity.Property(e => e.Status).HasMaxLength(20);
        entity.Property(e => e.FixType)
            .HasMaxLength(50)
            .HasColumnName("Fix_Type");
        entity.Property(e => e.FixTypeUid)
            .HasMaxLength(100)
            .HasColumnName("FixTypeUID");
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
        entity.HasOne(e => e.FixTypeNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.FixTypeUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.StoreNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.StoreUid)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.TechnicianNavigation)
            .WithMany(e => e.Quatations)
            .HasForeignKey(e => e.TechnicianUid)
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
            .HasMaxLength(50)
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
    /// 設定同步紀錄資料表欄位與索引。
    /// </summary>
    private static void ConfigureSyncLog(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SyncLog>();
        entity.ToTable("SyncLogs");
        entity.HasKey(e => e.Id);
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
        entity.Property(e => e.UpdatedAt)
            .HasColumnType("datetime");
        entity.HasIndex(e => new { e.TableName, e.StoreType, e.UpdatedAt });
        entity.HasIndex(e => e.Synced);
    }

    /// <summary>
    /// 設定門市同步狀態資料表欄位與索引。
    /// </summary>
    private static void ConfigureStoreSyncState(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StoreSyncState>();
        entity.ToTable("StoreSyncStates");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.StoreId)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.StoreType)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.LastCursor)
            .HasMaxLength(100);
        entity.Property(e => e.LastUploadTime)
            .HasColumnType("datetime");
        entity.Property(e => e.LastDownloadTime)
            .HasColumnType("datetime");
        entity.HasIndex(e => new { e.StoreId, e.StoreType })
            .IsUnique();
    }
}
