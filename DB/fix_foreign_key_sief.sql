-- Script para corregir el error de Foreign Key en SIEF
-- Crear la relación faltante entre aplicación SIEF y ambiente PROD

USE [SistemasComunes]
GO

-- Verificar si existe la relación SIEF-PROD
SELECT * FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
WHERE TM12_TM01_Codigo = 17 AND TM12_Ambiente = 'PROD'

-- Primero verificar estructura de la tabla
SELECT TOP 1 * FROM [dbo].[TM12_ConexionAplicacionXAmbiente]

-- Si no existe, crearla con servidor
IF NOT EXISTS (SELECT 1 FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
               WHERE TM12_TM01_Codigo = 17 AND TM12_Ambiente = 'PROD')
BEGIN
    INSERT INTO [dbo].[TM12_ConexionAplicacionXAmbiente] 
    ([TM12_TM01_Codigo], [TM12_Ambiente], [TM12_Servidor], [TM12_BaseDeDatos])
    VALUES (17, 'PROD', 'localhost', 'SIIR-ProdV1')
    
    PRINT '✅ Relación SIEF-PROD creada exitosamente'
END
ELSE
BEGIN
    PRINT '✅ Relación SIEF-PROD ya existe'
END

-- Verificar el resultado
SELECT 
    c.TM12_TM01_Codigo,
    a.TM01_Nombre AS Aplicacion,
    c.TM12_Ambiente
FROM [dbo].[TM12_ConexionAplicacionXAmbiente] c
INNER JOIN [dbo].[TM01_Aplicaciones] a ON c.TM12_TM01_Codigo = a.TM01_Codigo
WHERE c.TM12_TM01_Codigo = 17