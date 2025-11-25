USE [SIIR-ProdV1];
GO

/* ============================================================
   🔹 AGREGAR CAMPOS A [INSC].[TM08_ConsecutivoEnt]
   🔹 Autor: Alfredo Mamby Bossa
   🔹 Fecha: 2025-10-09
   ============================================================ */

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT '🚀 INICIO DE MODIFICACIÓN DE [INSC].[TM08_ConsecutivoEnt]';
    PRINT '============================================================';

    -- Agregar campo [TM08_TM01_Codigo] si no existe
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TM08_TM01_Codigo' 
        AND Object_ID = Object_ID(N'INSC.TM08_ConsecutivoEnt')
    )
    BEGIN
        ALTER TABLE [INSC].[TM08_ConsecutivoEnt]
        ADD [TM08_TM01_Codigo] INT NOT NULL DEFAULT 0;
        PRINT '✅ Columna [TM08_TM01_Codigo] agregada.';
    END

    -- Agregar campo [TM08_Ano] si no existe
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE Name = N'TM08_Ano' 
        AND Object_ID = Object_ID(N'INSC.TM08_ConsecutivoEnt')
    )
    BEGIN
        ALTER TABLE [INSC].[TM08_ConsecutivoEnt]
        ADD [TM08_Ano] INT NOT NULL DEFAULT YEAR(GETDATE());
        PRINT '✅ Columna [TM08_Ano] agregada.';
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
