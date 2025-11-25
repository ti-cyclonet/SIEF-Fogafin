-- Script para obtener información completa de las tablas necesarias

USE [SistemasComunes]
GO

PRINT '=== ESTRUCTURA TM12_ConexionAplicacionXAmbiente ==='
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TM12_ConexionAplicacionXAmbiente'
ORDER BY ORDINAL_POSITION

PRINT ''
PRINT '=== DATOS EXISTENTES TM12_ConexionAplicacionXAmbiente ==='
SELECT TOP 2 * FROM [dbo].[TM12_ConexionAplicacionXAmbiente]
WHERE TM12_Ambiente = 'PROD'

PRINT ''
PRINT '=== ESTRUCTURA TM15_ConexionAppAmbXResponsable ==='
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TM15_ConexionAppAmbXResponsable'
ORDER BY ORDINAL_POSITION

PRINT ''
PRINT '=== DATOS EXISTENTES TM15_ConexionAppAmbXResponsable ==='
SELECT TOP 2 * FROM [dbo].[TM15_ConexionAppAmbXResponsable]