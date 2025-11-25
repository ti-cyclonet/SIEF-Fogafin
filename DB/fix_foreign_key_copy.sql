-- Script para crear la relación SIEF-PROD copiando un registro existente

USE [SistemasComunes]
GO

-- Copiar un registro existente y modificarlo para SIEF-PROD
INSERT INTO [dbo].[TM12_ConexionAplicacionXAmbiente] 
SELECT 
    17 AS TM12_TM01_Codigo,  -- Código de SIEF
    'PROD' AS TM12_Ambiente, -- Ambiente PROD
    TM12_Servidor,
    TM12_BaseDeDatos,
    TM12_UsuarioBD,
    TM12_PasswordBD,
    TM12_Puerto,
    TM12_Activo,
    TM12_FechaCreacion,
    TM12_UsuarioCreacion,
    TM12_FechaModificacion,
    TM12_UsuarioModificacion
FROM [dbo].[TM12_ConexionAplicacionXAmbiente]
WHERE TM12_TM01_Codigo = (
    SELECT TOP 1 TM12_TM01_Codigo 
    FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
    WHERE TM12_Ambiente = 'PROD'
)
AND TM12_Ambiente = 'PROD'
AND NOT EXISTS (
    SELECT 1 FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
    WHERE TM12_TM01_Codigo = 17 AND TM12_Ambiente = 'PROD'
)

-- Verificar resultado
SELECT * FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
WHERE TM12_TM01_Codigo = 17