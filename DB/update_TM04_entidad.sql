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
           HACIA [INSC].[TM01_SECTORFINANCIERO]
       ============================================================ */
    PRINT '--- Migrando datos de [dbo].[TM01_SECTORFINANCIERO] a [INSC].[TM01_SECTORFINANCIERO] ---';

    IF OBJECT_ID('INSC.TM01_SectorFinanciero') IS NOT NULL
    BEGIN
        ;WITH SourceData AS (
            SELECT TM01_Codigo, TM01_Nombre, TM01_Descripcion, TM01_Estado, TM01_FechaCreacion
            FROM [dbo].[TM01_SectorFinanciero]
        )
        INSERT INTO [INSC].[TM01_SectorFinanciero] (TM01_Codigo, TM01_Nombre, TM01_Descripcion, TM01_Estado, TM01_FechaCreacion)
        SELECT s.TM01_Codigo, s.TM01_Nombre, s.TM01_Descripcion, s.TM01_Estado, s.TM01_FechaCreacion
        FROM SourceData s
        WHERE NOT EXISTS (
            SELECT 1 FROM [INSC].[TM01_SectorFinanciero] t
            WHERE t.TM01_Codigo = s.TM01_Codigo
        );

        DECLARE @Migrated INT = (SELECT COUNT(*) 
                                 FROM [dbo].[TM01_SectorFinanciero] s
                                 WHERE NOT EXISTS (
                                     SELECT 1 FROM [INSC].[TM01_SectorFinanciero] t 
                                     WHERE t.TM01_Codigo = s.TM01_Codigo
                                 ));
        PRINT CONCAT('✅ Registros migrados a [INSC].[TM01_SectorFinanciero]: ', @Migrated);

        DECLARE @Count_dbo INT = (SELECT COUNT(*) FROM [dbo].[TM01_SectorFinanciero]);
        DECLARE @Count_insc INT = (SELECT COUNT(*) FROM [INSC].[TM01_SectorFinanciero]);
        PRINT CONCAT('📊 Registros en [dbo]: ', @Count_dbo, ' | Registros en [INSC]: ', @Count_insc);
    END
    ELSE
    BEGIN
        PRINT '⚠️ La tabla [INSC].[TM01_SectorFinanciero] no existe. Se omite la migración.';
    END


    /* ============================================================
       1️⃣ Eliminación de campo obsoleto
       ============================================================ */
    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Tipo' 
        AND Object_ID = Object_ID(N'INSC.TN04_Entidad')
    )
    BEGIN
        ALTER TABLE [INSC].[TN04_Entidad]
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
        AND Object_ID = Object_ID(N'INSC.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'INSC.TN04_Entidad.TN04_Correo', 'TN04_Correo_Rep', 'COLUMN';
        PRINT '✅ Columna [TN04_Correo] → [TN04_Correo_Rep].';
    END

    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Telefono' 
        AND Object_ID = Object_ID(N'INSC.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'INSC.TN04_Entidad.TN04_Telefono', 'TN04_Telefono_Rep', 'COLUMN';
        PRINT '✅ Columna [TN04_Telefono] → [TN04_Telefono_Rep].';
    END

    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_Cargo' 
        AND Object_ID = Object_ID(N'INSC.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'INSC.TN04_Entidad.TN04_Cargo', 'TN04_Cargo_Rep', 'COLUMN';
        PRINT '✅ Columna [TN04_Cargo] → [TN04_Cargo_Rep].';
    END

    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TN04_TipoDoc' 
        AND Object_ID = Object_ID(N'INSC.TN04_Entidad')
    )
    BEGIN
        EXEC sp_rename 'INSC.TN04_Entidad.TN04_TipoDoc', 'TN04_TL15_Codigo', 'COLUMN';
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
            WHERE Name = @name AND Object_ID = Object_ID(N'INSC.TN04_Entidad')
        )
        BEGIN
            SET @sql = 'ALTER TABLE [INSC].[TN04_Entidad] ADD [' + @name + '] ' + @def + ';';
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
        AND parent_object_id = OBJECT_ID('INSC.TN04_Entidad')
    )
    BEGIN
        ALTER TABLE [INSC].[TN04_Entidad]
        ADD CONSTRAINT [FK_TN04_Entidad_TM01_SectorFinanciero]
        FOREIGN KEY ([TN04_TM01_CodigoSectorF])
        REFERENCES [INSC].[TM01_SectorFinanciero] ([TM01_Codigo]);
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


-- Script para crear tabla TM80_LOG_CORREOS
-- Tabla para registrar el log de todos los correos enviados por el sistema

CREATE TABLE [dbo].[TM80_LOG_CORREOS] (
    [TM80_ID] INT IDENTITY(1,1) NOT NULL,
    [TM80_TM02_CODIGO] INT NULL,
    [TM80_NUMERO_TRAMITE] NVARCHAR(50) NULL,
    [TM80_DESTINATARIOS] NVARCHAR(MAX) NOT NULL,
    [TM80_ASUNTO] NVARCHAR(500) NOT NULL,
    [TM80_CUERPO] NVARCHAR(MAX) NULL,
    [TM80_TIPO_CORREO] NVARCHAR(50) NOT NULL,
    [TM80_ESTADO_ENVIO] NVARCHAR(20) NOT NULL,
    [TM80_FECHA_ENVIO] DATETIME2(7) NOT NULL,
    [TM80_USUARIO] NVARCHAR(100) NULL,
    [TM80_ERROR_DETALLE] NVARCHAR(MAX) NULL,
    
    CONSTRAINT [PK_TM80_LOG_CORREOS] PRIMARY KEY CLUSTERED ([TM80_ID] ASC),
    CONSTRAINT [FK_TM80_TM02] FOREIGN KEY ([TM80_TM02_CODIGO]) REFERENCES [dbo].[TM02_ENTIDADFINANCIERA] ([TM02_CODIGO])
);

-- Índices para mejorar rendimiento
CREATE INDEX [IX_TM80_TM02_CODIGO] ON [dbo].[TM80_LOG_CORREOS] ([TM80_TM02_CODIGO]);
CREATE INDEX [IX_TM80_FECHA_ENVIO] ON [dbo].[TM80_LOG_CORREOS] ([TM80_FECHA_ENVIO]);
CREATE INDEX [IX_TM80_ESTADO_ENVIO] ON [dbo].[TM80_LOG_CORREOS] ([TM80_ESTADO_ENVIO]);

-- Comentarios de la tabla
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Tabla para registrar el log de todos los correos enviados por el sistema SIEF',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'TM80_LOG_CORREOS';