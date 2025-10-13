-- ------------------------------------------------------------
-- 觸發器建置腳本：於所有資料表異動時寫入 SyncLogs
-- 說明：執行此腳本會建立 INSERT/UPDATE/DELETE 觸發器，
--      將異動資料統一寫入 SyncLogs，供同步服務讀取差異。
--      請先確認 SyncLogs 資料表已存在，且具備欄位：
--      TableName、RecordId、Action、UpdatedAt、SourceServer、StoreType、Synced。
-- ------------------------------------------------------------
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_create_sync_log_triggers $$
CREATE PROCEDURE sp_create_sync_log_triggers()
BEGIN
    DECLARE done INT DEFAULT 0;
    DECLARE tableName VARCHAR(128);
    DECLARE pkExpressionNew LONGTEXT;
    DECLARE pkExpressionOld LONGTEXT;
    DECLARE pkList LONGTEXT;

    -- 只挑選一般業務資料表，排除同步與系統內部使用的資料表
    DECLARE cur CURSOR FOR
        SELECT TABLE_NAME
        FROM information_schema.tables
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_TYPE = 'BASE TABLE'
          AND TABLE_NAME NOT IN ('SyncLogs', 'StoreSyncStates', '__EFMigrationsHistory');
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;

    OPEN cur;

    read_loop: LOOP
        FETCH cur INTO tableName;
        IF done = 1 THEN
            LEAVE read_loop;
        END IF;

        -- 取得主鍵欄位並組成字串，若表格無主鍵則以 UUID() 代替
        SELECT GROUP_CONCAT(CONCAT('CAST(NEW.`', COLUMN_NAME, '` AS CHAR)')
                            ORDER BY ORDINAL_POSITION
                            SEPARATOR ', ')
          INTO pkList
          FROM information_schema.key_column_usage
         WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = tableName
           AND CONSTRAINT_NAME = 'PRIMARY';

        IF pkList IS NULL THEN
            SET pkExpressionNew = 'UUID()';
            SET pkExpressionOld = 'UUID()';
        ELSE
            SET pkExpressionNew = CONCAT('CONCAT_WS('':'', ', pkList, ')');
            SET pkExpressionOld = REPLACE(pkExpressionNew, 'NEW.`', 'OLD.`');
        END IF;

        -- 建立 INSERT 觸發器
        SET @dropTriggerSql = CONCAT('DROP TRIGGER IF EXISTS `trg_', tableName, '_sync_ai`');
        PREPARE stmt FROM @dropTriggerSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        SET @createTriggerSql = CONCAT(
            'CREATE TRIGGER `trg_', tableName, '_sync_ai` AFTER INSERT ON `', tableName, '` ',
            'FOR EACH ROW BEGIN ',
            'INSERT INTO `SyncLogs` (`TableName`, `RecordId`, `Action`, `UpdatedAt`, `SourceServer`, `StoreType`, `Synced`) ',
            'VALUES (''', tableName, ''', ', pkExpressionNew, ', ''INSERT'', NOW(6), NULL, NULL, 0); ',
            'END');
        PREPARE stmt FROM @createTriggerSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        -- 建立 UPDATE 觸發器
        SET @dropTriggerSql = CONCAT('DROP TRIGGER IF EXISTS `trg_', tableName, '_sync_au`');
        PREPARE stmt FROM @dropTriggerSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        SET @createTriggerSql = CONCAT(
            'CREATE TRIGGER `trg_', tableName, '_sync_au` AFTER UPDATE ON `', tableName, '` ',
            'FOR EACH ROW BEGIN ',
            'INSERT INTO `SyncLogs` (`TableName`, `RecordId`, `Action`, `UpdatedAt`, `SourceServer`, `StoreType`, `Synced`) ',
            'VALUES (''', tableName, ''', ', pkExpressionNew, ', ''UPDATE'', NOW(6), NULL, NULL, 0); ',
            'END');
        PREPARE stmt FROM @createTriggerSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        -- 建立 DELETE 觸發器
        SET @dropTriggerSql = CONCAT('DROP TRIGGER IF EXISTS `trg_', tableName, '_sync_ad`');
        PREPARE stmt FROM @dropTriggerSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;

        SET @createTriggerSql = CONCAT(
            'CREATE TRIGGER `trg_', tableName, '_sync_ad` AFTER DELETE ON `', tableName, '` ',
            'FOR EACH ROW BEGIN ',
            'INSERT INTO `SyncLogs` (`TableName`, `RecordId`, `Action`, `UpdatedAt`, `SourceServer`, `StoreType`, `Synced`) ',
            'VALUES (''', tableName, ''', ', pkExpressionOld, ', ''DELETE'', NOW(6), NULL, NULL, 0); ',
            'END');
        PREPARE stmt FROM @createTriggerSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END LOOP;

    CLOSE cur;
END $$

-- 呼叫程序建立觸發器
CALL sp_create_sync_log_triggers() $$

-- 建立完成後可將程序移除，避免重複存在
DROP PROCEDURE IF EXISTS sp_create_sync_log_triggers $$

DELIMITER ;
