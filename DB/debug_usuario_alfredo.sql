-- Script de diagnóstico para usuario 'AlfredoMamby'
-- Verificar por qué no aparece en la lista de usuarios disponibles para SIEF

USE [SistemasComunes]
GO

PRINT '=== DIAGNÓSTICO USUARIO: AlfredoMamby ==='
PRINT ''

-- 1. Verificar si existe en TM04_Responsables
PRINT '1. ¿Existe el usuario en TM04_Responsables?'
SELECT 
    TM04_Identificacion,
    TM04_Nombre,
    TM04_Apellidos,
    TM04_EMail,
    TM04_TM03_Codigo,
    TM04_Activo,
    CASE WHEN TM04_Activo = 1 THEN 'ACTIVO' ELSE 'INACTIVO' END AS Estado
FROM [dbo].[TM04_Responsables] 
WHERE TM04_Identificacion = 'AlfredoMamby'

IF @@ROWCOUNT = 0
    PRINT '❌ Usuario NO encontrado en TM04_Responsables'
ELSE
    PRINT '✅ Usuario encontrado en TM04_Responsables'

PRINT ''

-- 2. Verificar el área asignada
PRINT '2. ¿Qué área tiene asignada?'
SELECT 
    r.TM04_Identificacion,
    r.TM04_TM03_Codigo AS CodigoArea,
    s.TM03_Nombre AS NombreArea,
    CASE 
        WHEN r.TM04_TM03_Codigo IN (59030, 52060, 52050, 52070, 59010) THEN 'ÁREA VÁLIDA PARA SIEF'
        ELSE 'ÁREA NO VÁLIDA PARA SIEF'
    END AS ValidezArea
FROM [dbo].[TM04_Responsables] r
LEFT JOIN [dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
WHERE r.TM04_Identificacion = 'AlfredoMamby'

PRINT ''

-- 3. Verificar si ya tiene acceso a SIEF
PRINT '3. ¿Ya tiene acceso a SIEF (aplicación 17)?'
SELECT 
    TM15_TM04_Identificacion,
    TM15_TM12_TM01_Codigo AS CodigoAplicacion,
    TM15_TM12_Ambiente AS Ambiente,
    TM15_TM14_Perfil AS Perfil
FROM [dbo].[TM15_ConexionAppAmbXResponsable]
WHERE TM15_TM04_Identificacion = 'AlfredoMamby'
AND TM15_TM12_TM01_Codigo = 17

IF @@ROWCOUNT = 0
    PRINT '✅ Usuario NO tiene acceso a SIEF (puede ser activado)'
ELSE
    PRINT '❌ Usuario YA tiene acceso a SIEF (por eso no aparece en disponibles)'

PRINT ''

-- 4. Verificar todas las aplicaciones del usuario
PRINT '4. ¿A qué aplicaciones tiene acceso?'
SELECT 
    c.TM15_TM12_TM01_Codigo AS CodigoApp,
    a.TM01_Nombre AS NombreAplicacion,
    c.TM15_TM12_Ambiente AS Ambiente,
    c.TM15_TM14_Perfil AS Perfil
FROM [dbo].[TM15_ConexionAppAmbXResponsable] c
LEFT JOIN [dbo].[TM01_Aplicaciones] a ON c.TM15_TM12_TM01_Codigo = a.TM01_Codigo
WHERE c.TM15_TM04_Identificacion = 'AlfredoMamby'
ORDER BY c.TM15_TM12_TM01_Codigo

PRINT ''

-- 5. Ejecutar la consulta completa de usuarios disponibles para este usuario específico
PRINT '5. Resultado de la consulta de usuarios disponibles para AlfredoMamby:'
SELECT 
    r.TM04_Identificacion,
    r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
    r.TM04_EMail,
    s.TM03_Nombre AS Area,
    s.TM03_Codigo AS CodigoArea,
    r.TM04_Activo,
    CASE 
        WHEN r.TM04_TM03_Codigo IN (59030, 52060, 52050, 52070, 59010) THEN 'SÍ'
        ELSE 'NO'
    END AS AreaValida,
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM [dbo].[TM15_ConexionAppAmbXResponsable] c
            WHERE c.TM15_TM04_Identificacion = r.TM04_Identificacion 
            AND c.TM15_TM12_TM01_Codigo = 17
        ) THEN 'YA TIENE ACCESO'
        ELSE 'DISPONIBLE'
    END AS EstadoSIEF
FROM [dbo].[TM04_Responsables] r
INNER JOIN [dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
WHERE r.TM04_Identificacion = 'AlfredoMamby'

PRINT ''

-- 6. Mostrar códigos de áreas válidas para referencia
PRINT '6. Códigos de áreas válidas para SIEF:'
SELECT 
    TM03_Codigo,
    TM03_Nombre
FROM [dbo].[TM03_Subdirecciones]
WHERE TM03_Codigo IN (59030, 52060, 52050, 52070, 59010)
ORDER BY TM03_Codigo

PRINT ''
PRINT '=== FIN DEL DIAGNÓSTICO ==='