-- Script final para crear la relación SIEF-PROD con todos los campos obligatorios

USE [SistemasComunes]
GO

-- Crear la relación SIEF-PROD con todos los campos obligatorios
IF NOT EXISTS (SELECT 1 FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
               WHERE TM12_TM01_Codigo = 17 AND TM12_Ambiente = 'PROD')
BEGIN
    INSERT INTO [dbo].[TM12_ConexionAplicacionXAmbiente] 
    (
        [TM12_TM01_Codigo],     -- 17 (SIEF)
        [TM12_Ambiente],        -- PROD
        [TM12_Servidor],        -- localhost
        [TM12_BaseDeDatos],     -- SIIR-ProdV1
        [TM12_UsuarioBD],       -- sief_user
        [TM12_Contrasena],      -- password123
        [TM12_RestoCadena]      -- N/A
    )
    VALUES 
    (
        17,                     -- Código SIEF
        'PROD',                 -- Ambiente producción
        'localhost',            -- Servidor local
        'SIIR-ProdV1',         -- Base de datos SIEF
        'sief_user',           -- Usuario BD
        'password123',         -- Contraseña
        'N/A'                  -- Resto cadena
    )
    
    PRINT '✅ Relación SIEF-PROD creada exitosamente'
END
ELSE
BEGIN
    PRINT '✅ Relación SIEF-PROD ya existe'
END

-- Verificar resultado
SELECT * FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
WHERE TM12_TM01_Codigo = 17