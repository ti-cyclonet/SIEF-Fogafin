USE [SIIR-ProdV1];
GO

-- INICIO DE LA TRANSACCIÓN: Asegura que todos los DELETE se completen o ninguno lo haga.
BEGIN TRANSACTION;

-- 1. IDENTIFICAR LOS CÓDIGOS DE ENTIDADES DE PRUEBA (99900-99909)
-- Se usa una tabla variable para almacenar los códigos a eliminar
DECLARE @CodigosEntidadPrueba TABLE (Codigo INT PRIMARY KEY);

INSERT INTO @CodigosEntidadPrueba (Codigo)
SELECT TM02_Codigo
FROM dbo.TM02_ENTIDADFINANCIERA
WHERE TM02_Codigo BETWEEN 99900 AND 99909;

-- VERIFICAR SI HAY CÓDIGOS PARA ELIMINAR
IF EXISTS (SELECT 1 FROM @CodigosEntidadPrueba)
BEGIN
    -- ELIMINACIÓN EN CASCADA (Desde las tablas "hijas" a las "padres")

    -- Dependencias Directas de TM02_ENTIDADFINANCIERA:

    -- 2. TM64_LOG_NOTIFICACION (Log de notificaciones)
    DELETE FROM dbo.TM64_LOG_NOTIFICACION
    WHERE TM64_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 3. TM80_LOG_CORREOS (Log de correos)
    DELETE FROM dbo.TM80_LOG_CORREOS
    WHERE TM80_TM02_CODIGO IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 4. TN07_Adjuntos
    DELETE FROM dbo.TN07_Adjuntos
    WHERE TN07_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);
   
    -- 5. TN06_Pagos (NUEVA INCLUSIÓN)
    DELETE FROM dbo.TN06_Pagos
    WHERE TN06_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 6. TN05_Historico_Estado
    DELETE FROM dbo.TN05_Historico_Estado
    WHERE TN05_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 7. TM08_ConsecutivoEnt (Opcional - borra solo del año actual)
    DELETE FROM dbo.TM08_ConsecutivoEnt
    WHERE TM08_TM01_Codigo = 12 AND TM08_Ano = YEAR(GETDATE());

    -- Dependencias indirectas a través de TM61_ENTIDADES_NOTIFICACION:

    -- 8. TM63_DOCUMENTOS_NOTIFICACION (Depende de TM61_ENTIDADES_NOTIFICACION)
    DELETE FROM dbo.TM63_DOCUMENTOS_NOTIFICACION
    WHERE TM63_TM61_Codigo IN (
        SELECT TM61_Codigo 
        FROM dbo.TM61_ENTIDADES_NOTIFICACION 
        WHERE TM61_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba)
    );

    -- 9. TM61_ENTIDADES_NOTIFICACION (Depende directamente de TM02_ENTIDADFINANCIERA)
    DELETE FROM dbo.TM61_ENTIDADES_NOTIFICACION
    WHERE TM61_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 10. TM02_ENTIDADFINANCIERA (Tabla principal - debe ser la última)
    DELETE FROM dbo.TM02_ENTIDADFINANCIERA
    WHERE TM02_CODIGO IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    PRINT 'Eliminación completada y verificada para entidades de prueba (99900-99909).';
END
ELSE
BEGIN
    PRINT 'No se encontraron entidades de prueba (99900-99909) para eliminar.';
END

-- CONFIRMACIÓN DE LA TRANSACCIÓN
COMMIT TRANSACTION;
GO