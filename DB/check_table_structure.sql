USE [SIIR-ProdV1];
GO

-- Script para verificar la estructura de TM02_ENTIDADFINANCIERA
-- y identificar columnas obligatorias (NOT NULL)

PRINT '🔍 Verificando estructura de TM02_ENTIDADFINANCIERA...';

SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'dbo' 
AND TABLE_NAME = 'TM02_ENTIDADFINANCIERA'
AND IS_NULLABLE = 'NO'  -- Solo columnas NOT NULL
ORDER BY ORDINAL_POSITION;

PRINT '✅ Consulta completada. Revisar columnas NOT NULL que requieren valores.';