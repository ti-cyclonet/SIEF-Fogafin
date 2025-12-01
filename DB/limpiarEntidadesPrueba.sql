USE [SIIR-ProdV1];
GO

-- INICIO DE LA TRANSACCIÓN: Asegura que todos los DELETE se completen o ninguno lo haga.
BEGIN TRANSACTION;

-- 1. IDENTIFICAR LOS CÓDIGOS DE ENTIDADES DE PRUEBA (99900-99909)
DECLARE @CodigosEntidadPrueba TABLE (Codigo INT PRIMARY KEY);

INSERT INTO @CodigosEntidadPrueba (Codigo)
SELECT TM02_Codigo
FROM dbo.TM02_ENTIDADFINANCIERA
WHERE TM02_Codigo BETWEEN 99900 AND 99909;

-- VERIFICAR SI HAY CÓDIGOS PARA ELIMINAR
IF EXISTS (SELECT 1 FROM @CodigosEntidadPrueba)
BEGIN
    -- ELIMINACIÓN EN CASCADA (Desde las tablas "hijas" a las "padres")

    -- ¡Punto 2 (TN08_ExtractosPagos) ELIMINADO de este script!
    
    -- 2. TN09_Extractos (Depende de TN07) - Debe ir antes de TN07
    DELETE FROM dbo.TN09_Extractos
    WHERE TN09_TN07_Id IN (
        SELECT TN07_Id FROM dbo.TN07_Adjuntos
        WHERE TN07_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba)
    );

    -- Dependencias Directas de TM02_ENTIDADFINANCIERA (Las tablas "padres" de TN08 y TN09)

    -- 3. TN06_Pagos
    DELETE FROM dbo.TN06_Pagos
    WHERE TN06_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);
    
    -- 4. TN07_Adjuntos (Padre de TN09)
    DELETE FROM dbo.TN07_Adjuntos
    WHERE TN07_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- Resto de eliminaciones...
    -- 5. TM64_LOG_NOTIFICACION (Log de notificaciones)
    DELETE FROM dbo.TM64_LOG_NOTIFICACION
    WHERE TM64_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 6. TM80_LOG_CORREOS (Log de correos)
    DELETE FROM dbo.TM80_LOG_CORREOS
    WHERE TM80_TM02_CODIGO IN (SELECT Codigo FROM @CodigosEntidadPrueba);
   
    -- 7. TN05_Historico_Estado
    DELETE FROM dbo.TN05_Historico_Estado
    WHERE TN05_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 8. TM08_ConsecutivoEnt
    DELETE FROM dbo.TM08_ConsecutivoEnt
    WHERE TM08_TM01_Codigo = 12 AND TM08_Ano = YEAR(GETDATE());

    -- 9. TM63_DOCUMENTOS_NOTIFICACION
    DELETE FROM dbo.TM63_DOCUMENTOS_NOTIFICACION
    WHERE TM63_TM61_Codigo IN (
        SELECT TM61_Codigo 
        FROM dbo.TM61_ENTIDADES_NOTIFICACION 
        WHERE TM61_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba)
    );

    -- 10. TM61_ENTIDADES_NOTIFICACION
    DELETE FROM dbo.TM61_ENTIDADES_NOTIFICACION
    WHERE TM61_TM02_Codigo IN (SELECT Codigo FROM @CodigosEntidadPrueba);

    -- 11. TM02_ENTIDADFINANCIERA
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