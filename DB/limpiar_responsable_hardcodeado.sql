-- Limpiar datos hardcodeados de responsable y configurar usuarios SIEF correctos
USE [SIIR-ProdV1]
GO

-- 1. Limpiar campos de responsable hardcodeados en TM02_ENTIDADFINANCIERA
UPDATE dbo.TM02_ENTIDADFINANCIERA 
SET TM02_NombreResponsable = NULL,
    TM02_TelefonoResponsable = NULL
WHERE TM02_NombreResponsable = 'Alfredo Mamby Bossa'
   OR TM02_NombreResponsable LIKE '%Alfredo%'
   OR TM02_NombreResponsable LIKE '%Mamby%'
   OR TM02_NombreResponsable LIKE '%Bossa%'

PRINT 'Limpieza de responsables hardcodeados completada en TM02_ENTIDADFINANCIERA'

-- 2. Configurar usuarios SIEF activos
USE [SistemasComunes]
GO

-- Desactivar Alfredo Mamby Bossa si existe
UPDATE [dbo].[TM04_Responsables] 
SET TM04_Activo = 0
WHERE (TM04_Nombre LIKE '%Alfredo%' AND TM04_Apellidos LIKE '%Mamby%')
   OR (TM04_Nombre LIKE '%Alfredo%' AND TM04_Apellidos LIKE '%Bossa%')

-- Eliminar asignaciones SIEF de Alfredo Mamby Bossa
DELETE FROM [dbo].[TM15_ConexionAppAmbXResponsable]
WHERE TM15_TM12_TM01_Codigo = 17
  AND TM15_TM04_Identificacion IN (
    SELECT TM04_Identificacion 
    FROM [dbo].[TM04_Responsables]
    WHERE (TM04_Nombre LIKE '%Alfredo%' AND TM04_Apellidos LIKE '%Mamby%')
       OR (TM04_Nombre LIKE '%Alfredo%' AND TM04_Apellidos LIKE '%Bossa%')
  )

PRINT 'Usuario Alfredo Mamby Bossa desactivado y removido de SIEF'

-- 3. Insertar usuarios SIEF de ejemplo (si no existen)
-- Verificar si existen usuarios activos con perfiles SSD y DOT
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[TM04_Responsables] r
    INNER JOIN [dbo].[TM15_ConexionAppAmbXResponsable] c 
        ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
    WHERE c.TM15_TM12_TM01_Codigo = 17 
      AND c.TM15_TM14_Perfil = 'Profesional SSD'
      AND r.TM04_Activo = 1
)
BEGIN
    -- Insertar usuario SSD de ejemplo
    IF NOT EXISTS (SELECT 1 FROM [dbo].[TM04_Responsables] WHERE TM04_Identificacion = 'usuario.ssd')
    BEGIN
        INSERT INTO [dbo].[TM04_Responsables] 
        (TM04_Identificacion, TM04_Nombre, TM04_Apellidos, TM04_EMail, TM04_TM03_Codigo, TM04_Activo)
        VALUES 
        ('usuario.ssd', 'Funcionario', 'SSD Ejemplo', 'funcionario.ssd@fogafin.gov.co', 1, 1)
    END
    
    -- Asignar perfil SSD
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[TM15_ConexionAppAmbXResponsable] 
        WHERE TM15_TM04_Identificacion = 'usuario.ssd' AND TM15_TM12_TM01_Codigo = 17
    )
    BEGIN
        INSERT INTO [dbo].[TM15_ConexionAppAmbXResponsable] 
        (TM15_TM12_TM01_Codigo, TM15_TM12_Ambiente, TM15_TM14_Perfil, TM15_TM04_Identificacion)
        VALUES 
        (17, 'PROD', 'Profesional SSD', 'usuario.ssd')
    END
    
    PRINT 'Usuario SSD de ejemplo creado'
END

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[TM04_Responsables] r
    INNER JOIN [dbo].[TM15_ConexionAppAmbXResponsable] c 
        ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
    WHERE c.TM15_TM12_TM01_Codigo = 17 
      AND c.TM15_TM14_Perfil = 'Profesional DOT'
      AND r.TM04_Activo = 1
)
BEGIN
    -- Insertar usuario DOT de ejemplo
    IF NOT EXISTS (SELECT 1 FROM [dbo].[TM04_Responsables] WHERE TM04_Identificacion = 'usuario.dot')
    BEGIN
        INSERT INTO [dbo].[TM04_Responsables] 
        (TM04_Identificacion, TM04_Nombre, TM04_Apellidos, TM04_EMail, TM04_TM03_Codigo, TM04_Activo)
        VALUES 
        ('usuario.dot', 'Funcionario', 'DOT Ejemplo', 'funcionario.dot@fogafin.gov.co', 2, 1)
    END
    
    -- Asignar perfil DOT
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[TM15_ConexionAppAmbXResponsable] 
        WHERE TM15_TM04_Identificacion = 'usuario.dot' AND TM15_TM12_TM01_Codigo = 17
    )
    BEGIN
        INSERT INTO [dbo].[TM15_ConexionAppAmbXResponsable] 
        (TM15_TM12_TM01_Codigo, TM15_TM12_Ambiente, TM15_TM14_Perfil, TM15_TM04_Identificacion)
        VALUES 
        (17, 'PROD', 'Profesional DOT', 'usuario.dot')
    END
    
    PRINT 'Usuario DOT de ejemplo creado'
END

-- 4. Verificar configuración final
SELECT 
    r.TM04_Identificacion,
    r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
    c.TM15_TM14_Perfil AS Perfil,
    CASE WHEN r.TM04_Activo = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado
FROM [dbo].[TM04_Responsables] r
INNER JOIN [dbo].[TM15_ConexionAppAmbXResponsable] c 
    ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
WHERE c.TM15_TM12_TM01_Codigo = 17 
  AND c.TM15_TM12_Ambiente = 'PROD'
ORDER BY c.TM15_TM14_Perfil, r.TM04_Identificacion

PRINT 'Configuración de usuarios SIEF completada'