-- Script completo para crear la relación SIEF-PROD con todos los campos

USE [SistemasComunes]
GO

-- Insertar con todos los campos obligatorios
IF NOT EXISTS (SELECT 1 FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
               WHERE TM12_TM01_Codigo = 17 AND TM12_Ambiente = 'PROD')
BEGIN
    INSERT INTO [dbo].[TM12_ConexionAplicacionXAmbiente] 
    ([TM12_TM01_Codigo], [TM12_Ambiente], [TM12_Servidor], [TM12_BaseDeDatos], 
     [TM12_UsuarioBD], [TM12_PasswordBD], [TM12_Puerto], [TM12_Activo])
    VALUES 
    (17, 'PROD', 'localhost', 'SIIR-ProdV1', 'sief_user', 'password123', 1433, 1)
    
    PRINT '✅ Relación SIEF-PROD creada exitosamente'
END
ELSE
BEGIN
    PRINT '✅ Relación SIEF-PROD ya existe'
END

-- Verificar resultado
SELECT * FROM [dbo].[TM12_ConexionAplicacionXAmbiente] 
WHERE TM12_TM01_Codigo = 17