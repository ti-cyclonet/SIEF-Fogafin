-- Verificar perfiles SIEF en TM14_PerfilesAplicacion

USE [SistemasComunes]
GO

-- Ver perfiles existentes para SIEF (aplicación 17)
SELECT * FROM [dbo].[TM14_PerfilesAplicacion] 
WHERE [TM14_TM01_Codigo] = 17

-- Ver todos los perfiles para comparar
SELECT TOP 5 * FROM [dbo].[TM14_PerfilesAplicacion] 
ORDER BY [TM14_TM01_Codigo]