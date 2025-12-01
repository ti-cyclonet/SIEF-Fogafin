-- ============================================
-- MIGRACIÓN DE DATOS DESDE [InscripcionesEnt].[dbo]
-- HACIA [SIIR-ProdV1].[INSC]
-- Tablas: TM02_Area y TM01_Estado
-- ============================================

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '--- Iniciando migración de TM02_Area ---';

    -- 1️⃣ Migrar datos de TM02_Area
    INSERT INTO [SIIR-ProdV1].[INSC].[TM02_Area] (
        TM02_Codigo,
        TM02_Nombre
    )
    SELECT 
        src.TM02_Codigo,
        src.TM02_Nombre
    FROM [InscripcionesEnt].[dbo].[TM02_Area] src
    WHERE src.TM02_Codigo NOT IN (
        SELECT dest.TM02_Codigo
        FROM [SIIR-ProdV1].[INSC].[TM02_Area] dest
    );

    PRINT '✅ Migración de TM02_Area completada correctamente.';


    PRINT '--- Iniciando migración de TM01_Estado ---';

    -- 2️⃣ Activar IDENTITY_INSERT manualmente
    SET IDENTITY_INSERT [SIIR-ProdV1].[INSC].[TM01_Estado] ON;

    INSERT INTO [SIIR-ProdV1].[INSC].[TM01_Estado] (
        TM01_Codigo,
        TM01_TM01_Codigo,
        TM01_Nombre,
        TM01_TM02_Codigo
    )
    SELECT 
        src.TM01_Codigo,
        src.TM01_TM01_Codigo,
        src.TM01_Nombre,
        src.TM01_TM02_Codigo
    FROM [InscripcionesEnt].[dbo].[TM01_Estado] src
    WHERE src.TM01_Codigo NOT IN (
        SELECT dest.TM01_Codigo
        FROM [SIIR-ProdV1].[INSC].[TM01_Estado] dest
    );

    -- 3️⃣ Desactivar IDENTITY_INSERT
    SET IDENTITY_INSERT [SIIR-ProdV1].[INSC].[TM01_Estado] OFF;

    PRINT '✅ Migración de TM01_Estado completada correctamente.';


    PRINT '--- Verificando existencia del área 59030 ---';

    -- 4️⃣ Crear el área si no existe (para evitar error de FK)
    IF NOT EXISTS (
        SELECT 1 
        FROM [SIIR-ProdV1].[INSC].[TM02_Area]
        WHERE TM02_Codigo = 59030
    )
    BEGIN
        INSERT INTO [SIIR-ProdV1].[INSC].[TM02_Area] (TM02_Codigo, TM02_Nombre)
        VALUES (59030, N'Área de Validación de Documentos');

        PRINT '✅ Área 59030 creada exitosamente.';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ El área 59030 ya existe, no se creó nuevamente.';
    END


    PRINT '--- Insertando nuevo estado personalizado ---';

    -- 5️⃣ Insertar estado adicional "En validación de documentos"
    IF NOT EXISTS (
        SELECT 1 
        FROM [SIIR-ProdV1].[INSC].[TM01_Estado]
        WHERE TM01_Nombre = N'En validación de documentos'
    )
    BEGIN
        INSERT INTO [SIIR-ProdV1].[INSC].[TM01_Estado] (
            TM01_TM01_Codigo,
            TM01_Nombre,
            TM01_TM02_Codigo
        )
        VALUES (
            0,
            N'En validación de documentos',
            59030
        );

        PRINT '✅ Estado "En validación de documentos" insertado correctamente.';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ El estado "En validación de documentos" ya existe, no se insertó.';
    END


    COMMIT TRANSACTION;
    PRINT '🎉 Migración general completada exitosamente.';

END TRY
BEGIN CATCH
    PRINT '❌ Error durante la migración.';
    PRINT ERROR_MESSAGE();
    ROLLBACK TRANSACTION;
END CATCH;
