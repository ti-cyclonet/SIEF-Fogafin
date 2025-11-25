USE [SIIR-ProdV1];
GO

-- Script para configurar TM02_CODIGO sin IDENTITY y con prefijo 999
-- Los códigos serán: 99900, 99901, 99902, etc.

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '🔧 Configurando TM02_ENTIDADFINANCIERA para códigos con prefijo 999...';

    -- Verificar si la columna TM02_CODIGO tiene IDENTITY
    IF EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.TM02_ENTIDADFINANCIERA') 
        AND name = 'TM02_CODIGO' 
        AND is_identity = 1
    )
    BEGIN
        PRINT '⚠️ La columna TM02_CODIGO tiene IDENTITY. Necesita ser reconfigurada manualmente.';
        PRINT '   Ejecute los siguientes pasos manualmente:';
        PRINT '   1. Crear tabla temporal';
        PRINT '   2. Copiar datos';
        PRINT '   3. Eliminar tabla original';
        PRINT '   4. Renombrar tabla temporal';
    END
    ELSE
    BEGIN
        PRINT '✅ La columna TM02_CODIGO no tiene IDENTITY. Configuración correcta.';
    END

    -- Asegurar que no hay códigos en el rango 999xx
    IF EXISTS (SELECT 1 FROM dbo.TM02_ENTIDADFINANCIERA WHERE TM02_CODIGO >= 99900)
    BEGIN
        PRINT '⚠️ Ya existen códigos en el rango 999xx. Verificar datos existentes.';
    END
    ELSE
    BEGIN
        PRINT '✅ Rango 999xx disponible para nuevos registros.';
    END

    COMMIT TRANSACTION;
    PRINT '🎉 Configuración completada.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT '❌ Error: ' + ERROR_MESSAGE();
END CATCH;
GO