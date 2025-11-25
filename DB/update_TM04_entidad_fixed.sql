USE [SIIR-ProdV1];
GO

/* ============================================================
   🔹 MIGRACIÓN DE DATOS Y MODIFICACIONES DE TABLAS
   🔹 Autor: Alfredo Mamby Bossa
   🔹 Fecha: 2025-10-08
   ============================================================ */

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT '🚀 INICIO DE MIGRACIÓN Y MODIFICACIONES DE TABLAS';
    PRINT '============================================================';


    /* ============================================================
       0️⃣ MIGRAR DATOS DESDE [dbo].[TM01_SECTORFINANCIERO]
           HACIA [dbo].[TM01_SECTORFINANCIERO]
       ============================================================ */
    PRINT '--- Migrando datos de [dbo].[TM01_SECTORFINANCIERO] a [dbo].[TM01_SECTORFINANCIERO] ---';

    IF OBJECT_ID('dbo.TM01_SectorFinanciero') IS NOT NULL
    BEGIN
        ;WITH SourceData AS (
            SELECT TM01_Codigo, TM01_Nombre, TM01_Descripcion, TM01_Estado, TM01_FechaCreacion
            FROM [dbo].[TM01_SectorFinanciero]
        )
        INSERT INTO [dbo].[TM01_SectorFinanciero] (TM01_Codigo, TM01_Nombre, TM01_Descripcion, TM01_Estado, TM01_FechaCreacion)
        SELECT s.TM01_Codigo, s.TM01_Nombre, s.TM01_Descripcion, s.TM01_Estado, s.TM01_FechaCreacion
        FROM SourceData s
        WHERE NOT EXISTS (
            SELECT 1 FROM [dbo].[TM01_SectorFinanciero] t
            WHERE t.TM01_Codigo = s.TM01_Codigo
        );

        DECLARE @Migrated INT = (SELECT COUNT(*) 
                                 FROM [dbo].[TM01_SectorFinanciero] s
                                 WHERE NOT EXISTS (
                                     SELECT 1 FROM [dbo].[TM01_SectorFinanciero] t 
                                     WHERE t.TM01_Codigo = s.TM01_Codigo
                                 ));
        PRINT CONCAT('✅ Registros migrados a [dbo].[TM01_SectorFinanciero]: ', @Migrated);

        DECLARE @Count_dbo INT = (SELECT COUNT(*) FROM [dbo].[TM01_SectorFinanciero]);
        DECLARE @Count_insc INT = (SELECT COUNT(*) FROM [dbo].[TM01_SectorFinanciero]);
        PRINT CONCAT('📊 Registros en [dbo]: ', @Count_dbo, ' | Registros en [dbo]: ', @Count_insc);
    END
    ELSE
    BEGIN
        PRINT '⚠️ La tabla [dbo].[TM01_SectorFinanciero] no existe. Se omite la migración.';
    END


    /* ============================================================
       1️⃣ Eliminación de campo obsoleto
       ============================================================ */
    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Tipo' 
        AND Object_ID = Object_ID(N'dbo.TN04_Entidad')
    )
    BEGIN
        ALTER TABLE [dbo].[TN04_Entidad]
        DROP COLUMN [TN04_Tipo];
        PRINT '🗑️ Campo [TN04_Tipo] eliminado correctamente.';
    END


    /* ============================================================
       2️⃣ Renombrar columnas existentes
       ============================================================ */
    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Correo' 
        AND Object_ID = Object_ID(N'dbo.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'dbo.TN04_Entidad.TN04_Correo', 'TN04_Correo_Rep', 'COLUMN';
        PRINT '✅ Columna [TN04_Correo] → [TN04_Correo_Rep].';
    END

    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Telefono' 
        AND Object_ID = Object_ID(N'dbo.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'dbo.TN04_Entidad.TN04_Telefono', 'TN04_Telefono_Rep', 'COLUMN';
        PRINT '✅ Columna [TN04_Telefono] → [TN04_Telefono_Rep].';
    END

    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Cargo' 
        AND Object_ID = Object_ID(N'dbo.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'dbo.TN04_Entidad.TN04_Cargo', 'TN04_Cargo_Rep', 'COLUMN';
        PRINT '✅ Columna [TN04_Cargo] → [TN04_Cargo_Rep].';
    END

    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_TipoDoc' 
        AND Object_ID = Object_ID(N'dbo.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'dbo.TN04_Entidad.TN04_TipoDoc', 'TN04_TL15_Codigo', 'COLUMN';
        PRINT '✅ Columna [TN04_TipoDoc] → [TN04_TL15_Codigo].';
    END


    /* ============================================================
       3️⃣ Agregar nuevas columnas (si no existen)
       ============================================================ */
    DECLARE @cols TABLE (ColName NVARCHAR(100), SqlDef NVARCHAR(MAX));

    INSERT INTO @cols VALUES
    ('TN04_Correo_Noti',         'NVARCHAR(150) NULL'),
    ('TN04_PaginaWeb',           'NVARCHAR(255) NULL'),
    ('TN04_RutaLogoEntidad',     'NVARCHAR(500) NULL'),
    ('TN04_ValorPagado',         'DECIMAL(18,2) NULL'),
    ('TN04_FechaPago',           'DATETIME NULL'),
    ('TN04_TelefonoResponsable', 'NVARCHAR(50) NULL'),
    ('TN04_TM01_CodigoSectorF',  'INT NULL'),
    ('TN04_TM08_Consecutivo',    'INT NOT NULL DEFAULT 0'),
    ('TN04_RutaResumenPdf',      'NVARCHAR(500) NULL');

    DECLARE @name NVARCHAR(100), @def NVARCHAR(MAX), @sql NVARCHAR(MAX);
    DECLARE cur CURSOR FOR SELECT ColName, SqlDef FROM @cols;
    OPEN cur;
    FETCH NEXT FROM cur INTO @name, @def;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = @name AND Object_ID = Object_ID(N'dbo.TN04_Entidad')
        )
        BEGIN
            SET @sql = 'ALTER TABLE [dbo].[TN04_Entidad] ADD [' + @name + '] ' + @def + ';';
            EXEC(@sql);
            PRINT CONCAT('✅ Columna [', @name, '] agregada.');
        END
        FETCH NEXT FROM cur INTO @name, @def;
    END
    CLOSE cur; DEALLOCATE cur;


    /* ============================================================
       4️⃣ Crear la relación (FOREIGN KEY)
       ============================================================ */
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.foreign_keys 
        WHERE name = 'FK_TN04_Entidad_TM01_SectorFinanciero'
        AND parent_object_id = OBJECT_ID('dbo.TN04_Entidad')
    )
    BEGIN
        ALTER TABLE [dbo].[TN04_Entidad]
        ADD CONSTRAINT [FK_TN04_Entidad_TM01_SectorFinanciero]
        FOREIGN KEY ([TN04_TM01_CodigoSectorF])
        REFERENCES [dbo].[TM01_SectorFinanciero] ([TM01_Codigo]);
        PRINT '🔗 Clave foránea [FK_TN04_Entidad_TM01_SectorFinanciero] creada correctamente.';
    END


    COMMIT TRANSACTION;
    PRINT '============================================================';
    PRINT '🎉 PROCESO COMPLETADO EXITOSAMENTE';
    PRINT '============================================================';


END TRY
BEGIN CATCH
    PRINT '❌ Error durante la ejecución.';
    PRINT ERROR_MESSAGE();
    ROLLBACK TRANSACTION;
END CATCH;
GO