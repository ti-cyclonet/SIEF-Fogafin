-- Script para analizar la estructura de la tabla TM12_ConexionAplicacionXAmbiente

USE [SistemasComunes]
GO

-- Ver estructura completa de la tabla
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TM12_ConexionAplicacionXAmbiente'
ORDER BY ORDINAL_POSITION

PRINT ''
PRINT '=== REGISTROS EXISTENTES ==='

-- Ver algunos registros existentes para usar como plantilla
SELECT TOP 3 * FROM [dbo].[TM12_ConexionAplicacionXAmbiente]
WHERE TM12_Ambiente = 'PROD'