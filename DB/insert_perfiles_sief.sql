-- Insertar perfiles SIEF faltantes en TM14_PerfilesAplicacion

USE [SistemasComunes]
GO

-- Insertar los perfiles SIEF que faltan
INSERT INTO [dbo].[TM14_PerfilesAplicacion] ([TM14_TM01_Codigo], [TM14_Perfil], [TM14_Descripcion])
VALUES 
(17, 'Jefe SSD', 'Perfil encargado de la aprobación de solicitudes, edición de información y cargue de documentos'),
(17, 'Profesional SSD', 'Perfil habilitado para la consulta de trámites y cargue de documentos'),
(17, 'Jefe DOT', 'Perfil habilitado para la consulta de trámites, cargue de documentos y cambios de estado'),
(17, 'Profesional DOT', 'Perfil habilitado para la consulta de trámites, cargue de documentos y cambios de estado'),
(17, 'Consulta', 'Perfil habilitado para consultar los datos del trámite')

-- Verificar que se insertaron correctamente
SELECT * FROM [dbo].[TM14_PerfilesAplicacion] 
WHERE [TM14_TM01_Codigo] = 17
ORDER BY [TM14_Perfil]