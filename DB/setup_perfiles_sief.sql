-- Script para configurar perfiles SIEF en SistemasComunes
USE [SistemasComunes]
GO

-- Insertar perfiles para la aplicación SIEF (código 17)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TM14_PerfilesAplicacion] WHERE [TM14_TM01_Codigo] = 17)
BEGIN
    INSERT INTO [dbo].[TM14_PerfilesAplicacion] ([TM14_TM01_Codigo], [TM14_Perfil], [TM14_Descripcion])
    VALUES 
    (17, 'Jefe SSD', 'Perfil encargado de la aprobación de solicitudes, edición de información y cargue de documentos'),
    (17, 'Profesional SSD', 'Perfil habilitado para la consulta de trámites y cargue de documentos'),
    (17, 'Jefe DOT', 'Perfil habilitado para la consulta de trámites, cargue de documentos y cambios de estado'),
    (17, 'Profesional DOT', 'Perfil habilitado para la consulta de trámites, cargue de documentos y cambios de estado'),
    (17, 'Consulta', 'Perfil habilitado para consultar los datos del trámite');
END

-- Configurar perfiles en SIIR-ProdV1
USE [SIIR-ProdV1]
GO

-- Insertar perfiles en TM19_PERFIL
IF NOT EXISTS (SELECT 1 FROM [dbo].[TM19_PERFIL] WHERE [TM19_NOMBRE] = 'Jefe SSD')
BEGIN
    INSERT INTO [dbo].[TM19_PERFIL] ([TM19_NOMBRE], [TM19_DESCRIPCION])
    VALUES 
    ('Jefe SSD', 'Perfil encargado de la aprobación de solicitudes, edición de información y cargue de documentos'),
    ('Profesional SSD', 'Perfil habilitado para la consulta de trámites y cargue de documentos'),
    ('Jefe DOT', 'Perfil habilitado para la consulta de trámites, cargue de documentos y cambios de estado'),
    ('Profesional DOT', 'Perfil habilitado para la consulta de trámites, cargue de documentos y cambios de estado'),
    ('Consulta', 'Perfil habilitado para consultar los datos del trámite');
END

-- Ejemplo de asignación de usuario a aplicación SIEF
-- Para asignar un usuario existente al sistema SIEF:
/*
USE [SistemasComunes]
GO

-- Asignar usuario a SIEF con perfil específico
INSERT INTO [dbo].[TM15_ConexionAppAmbXResponsable] 
([TM15_TM12_TM01_Codigo], [TM15_TM12_Ambiente], [TM15_TM14_Perfil], [TM15_TM04_Identificacion])
VALUES 
(17, 'PROD', 'Jefe SSD', 'usuario.ejemplo');
*/

-- Crear vista para consulta integrada de usuarios SIEF
USE [SistemasComunes]
GO

CREATE OR ALTER VIEW [dbo].[VW_USUARIOS_SIEF] AS
SELECT 
    r.TM04_Identificacion,
    r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
    r.TM04_EMail,
    s.TM03_Nombre AS Area,
    s.TM03_Codigo AS CodigoArea,
    c.TM15_TM14_Perfil AS Perfil,
    CASE WHEN r.TM04_Activo = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado,
    p.TM14_Descripcion AS DescripcionPerfil
FROM [dbo].[TM04_Responsables] r
INNER JOIN [dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
INNER JOIN [dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
INNER JOIN [dbo].[TM14_PerfilesAplicacion] p ON c.TM15_TM14_Perfil = p.TM14_Perfil AND p.TM14_TM01_Codigo = 17
WHERE c.TM15_TM12_TM01_Codigo = 17
  AND c.TM15_TM12_Ambiente = 'PROD';
GO