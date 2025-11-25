USE [SIIR-ProdV1];
GO

PRINT '============================================================';
PRINT '   🚀 MIGRACIÓN DE [dbo].[TM01_SectorFinanciero] → [INSC].[TM01_SectorFinanciero]';
PRINT '============================================================';
GO

BEGIN TRY
    BEGIN TRANSACTION;

    ------------------------------------------------------------
    -- 1️⃣ Eliminar claves foráneas que apuntan a la tabla destino
    ------------------------------------------------------------
    DECLARE @fkName NVARCHAR(128), @parentSchema NVARCHAR(128), @parentTable NVARCHAR(128), @sql NVARCHAR(MAX);

    DECLARE fk_cursor CURSOR FOR
    SELECT fk.name AS FKName,
           sch.name AS SchemaName,
           t.name AS TableName
    FROM sys.foreign_keys fk
    INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
    INNER JOIN sys.schemas sch ON t.schema_id = sch.schema_id
    WHERE fk.referenced_object_id = OBJECT_ID('INSC.TM01_SectorFinanciero');

    OPEN fk_cursor;
    FETCH NEXT FROM fk_cursor INTO @fkName, @parentSchema, @parentTable;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = 'ALTER TABLE [' + @parentSchema + '].[' + @parentTable + '] DROP CONSTRAINT [' + @fkName + ']';
        EXEC(@sql);
        PRINT '✅ Clave foránea ' + @fkName + ' eliminada temporalmente.';
        FETCH NEXT FROM fk_cursor INTO @fkName, @parentSchema, @parentTable;
    END

    CLOSE fk_cursor;
    DEALLOCATE fk_cursor;

    ------------------------------------------------------------
    -- 2️⃣ Eliminar tabla destino si ya existe
    ------------------------------------------------------------
    IF OBJECT_ID('INSC.TM01_SectorFinanciero', 'U') IS NOT NULL
    BEGIN
        DROP TABLE [INSC].[TM01_SectorFinanciero];
        PRINT '✅ Tabla [INSC].[TM01_SectorFinanciero] eliminada correctamente.';
    END

    ------------------------------------------------------------
    -- 3️⃣ Crear tabla destino copiando estructura exacta
    ------------------------------------------------------------
    SELECT TOP 0 *
    INTO [INSC].[TM01_SectorFinanciero]
    FROM [dbo].[TM01_SectorFinanciero];

    PRINT '✅ Tabla [INSC].[TM01_SectorFinanciero] creada con la estructura de [dbo].[TM01_SectorFinanciero].';

    ------------------------------------------------------------
    -- 4️⃣ Migrar datos
    ------------------------------------------------------------
    DECLARE @cols NVARCHAR(MAX);

    SELECT @cols = STRING_AGG(QUOTENAME(COLUMN_NAME), ', ')
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'TM01_SectorFinanciero';

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('INSC.TM01_SectorFinanciero') AND is_identity = 1
    )
        SET IDENTITY_INSERT [INSC].[TM01_SectorFinanciero] ON;

    EXEC('INSERT INTO [INSC].[TM01_SectorFinanciero] (' + @cols + ')
          SELECT ' + @cols + ' FROM [dbo].[TM01_SectorFinanciero]');

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('INSC.TM01_SectorFinanciero') AND is_identity = 1
    )
        SET IDENTITY_INSERT [INSC].[TM01_SectorFinanciero] OFF;

    PRINT '✅ Datos migrados correctamente desde [dbo].[TM01_SectorFinanciero].';

    COMMIT TRANSACTION;
    PRINT '🎉 Migración completada exitosamente.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT '❌ Error durante la migración.';
    PRINT ERROR_MESSAGE();
END CATCH;
GO
