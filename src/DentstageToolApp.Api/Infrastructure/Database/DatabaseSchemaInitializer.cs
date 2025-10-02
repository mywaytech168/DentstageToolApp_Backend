using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace DentstageToolApp.Api.Infrastructure.Database;

/// <summary>
/// 資料庫結構初始化服務，專責偵測並補齊舊版資料庫缺少的必要欄位。
/// </summary>
public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
{
    private readonly DentstageToolAppContext _context;
    private readonly ILogger<DatabaseSchemaInitializer> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與日誌服務，便於後續紀錄初始化過程。
    /// </summary>
    public DatabaseSchemaInitializer(DentstageToolAppContext context, ILogger<DatabaseSchemaInitializer> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task EnsureQuotationTechnicianColumnAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string tableName = "Quatations";
        const string columnName = "TechnicianUID";

        try
        {
            // 透過資料庫連線直接檢查 information_schema，判斷欄位是否已存在。
            await using var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText =
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName;";
                checkCommand.CommandType = CommandType.Text;
                checkCommand.Parameters.Add(new MySqlParameter("@tableName", tableName));
                checkCommand.Parameters.Add(new MySqlParameter("@columnName", columnName));

                var result = await checkCommand.ExecuteScalarAsync(cancellationToken);
                var columnExists = Convert.ToInt32(result ?? 0, provider: null) > 0;

                if (columnExists)
                {
                    // 若欄位已存在則直接結束，避免重複執行 ALTER TABLE。
                    _logger.LogInformation("估價單資料表欄位 {ColumnName} 已存在，略過補強流程。", columnName);
                    return;
                }
            }

            // 建立補齊欄位的指令，確保新程式可以存取技師 UID 欄位。
            await using (var alterCommand = connection.CreateCommand())
            {
                alterCommand.CommandText =
                    "ALTER TABLE `Quatations` ADD COLUMN `TechnicianUID` VARCHAR(100) NULL AFTER `UserName`;";
                alterCommand.CommandType = CommandType.Text;
                await alterCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogInformation("成功於估價單資料表新增欄位 {ColumnName}，確保估價單可儲存技師 UID。", columnName);
        }
        catch (Exception ex)
        {
            // 以錯誤日誌明確記錄失敗原因並重新拋出，讓啟動程序可感知異常。
            _logger.LogError(ex, "補齊估價單欄位 {ColumnName} 時發生例外，請檢查資料庫權限或結構。", columnName);
            throw;
        }
    }
}
