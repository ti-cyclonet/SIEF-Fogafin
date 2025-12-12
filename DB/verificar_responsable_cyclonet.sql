-- Verificar datos del trámite de Cyclonet y usuarios SIEF
USE [SIIR-ProdV1]
GO

-- 1. Verificar datos de la entidad Cyclonet
SELECT 
    TM02_CODIGO,
    TM02_NOMBRE,
    TM02_NIT,
    TM02_TM08_Consecutivo,
    TM02_TM01_CodigoSectorF,
    TM02_Fecha,
    TM02_NombreResponsable,
    TM02_TelefonoResponsable
FROM dbo.TM02_ENTIDADFINANCIERA 
WHERE TM02_NIT = '901515884'

-- 2. Verificar estado actual del trámite
SELECT TOP 1
    he.TN05_TM02_Codigo,
    he.TN05_TM01_EstadoActual,
    TM1.TM01_Nombre AS EstadoNombre,
    he.TN05_Fecha
FROM TN05_Historico_Estado he
INNER JOIN dbo.TM01_Estado TM1 ON he.TN05_TM01_EstadoActual = TM1.TM01_Codigo
INNER JOIN dbo.TM02_ENTIDADFINANCIERA TM2 ON he.TN05_TM02_Codigo = TM2.TM02_CODIGO
WHERE TM2.TM02_NIT = '901515884'
ORDER BY he.TN05_Fecha DESC

-- 3. Verificar usuarios SIEF activos
USE [SistemasComunes]
GO

SELECT 
    r.TM04_Identificacion,
    r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
    r.TM04_EMail,
    c.TM15_TM14_Perfil AS Perfil,
    r.TM04_Activo,
    CASE WHEN r.TM04_Activo = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado
FROM [dbo].[TM04_Responsables] r
INNER JOIN [dbo].[TM15_ConexionAppAmbXResponsable] c 
    ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
WHERE c.TM15_TM12_TM01_Codigo = 17 
  AND c.TM15_TM12_Ambiente = 'PROD'
ORDER BY c.TM15_TM14_Perfil, r.TM04_Identificacion

-- 4. Verificar específicamente Alfredo Mamby Bossa
SELECT 
    r.TM04_Identificacion,
    r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
    c.TM15_TM14_Perfil AS Perfil,
    r.TM04_Activo,
    CASE WHEN r.TM04_Activo = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado
FROM [dbo].[TM04_Responsables] r
INNER JOIN [dbo].[TM15_ConexionAppAmbXResponsable] c 
    ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
WHERE (r.TM04_Nombre LIKE '%Alfredo%' OR r.TM04_Apellidos LIKE '%Mamby%' OR r.TM04_Apellidos LIKE '%Bossa%')
  AND c.TM15_TM12_TM01_Codigo = 17