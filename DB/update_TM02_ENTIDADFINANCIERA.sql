-- Agrega los campos del formulario de inscripción de Entidad Financiera a la tabla TM02_ENTIDADFINANCIERA
-- *********************************************************************************************************
ALTER TABLE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
ADD 
    [TN04_Identificacion_Rep] varchar(20) NULL,
    [TN04_TL15_Codigo] varchar(10) NULL,
    [TN04_Nombre_Rep] varchar(100) NULL,
    [TN04_Apellido_Rep] varchar(100) NULL,
    [TN04_Cargo_Rep] varchar(100) NULL,
    [TN04_Correo_Rep] varchar(150) NULL,
    [TN04_Telefono_Rep] varchar(50) NULL,
    [TN04_Fecha] datetime NULL,
    [TN04_CartaSolicitud] varchar(255) NULL,
    [TN04_CertificadoSuper] varchar(255) NULL,
    [TN04_CartaCalidad] varchar(255) NULL,
    [TN04_NombreResponsable] varchar(100) NULL,
    [TN04_CorreoResponsable] varchar(150) NULL,
    [TN04_TelefonoResponsable] varchar(50) NULL,
    [TN04_Usuario] varchar(50) NULL,
    [TN04_Password] varchar(100) NULL,
    [TN04_RutaCartaAceptacion] varchar(255) NULL,
    [TN04_FechaCartaAceptacion] datetime NULL,
    [TN04_FechaLimitePago] datetime NULL,
    [TN04_FechaConstitucion] datetime NULL,
    [TN04_CapitalSuscrito] decimal(18, 2) NULL,
    [TN04_Ciudad] varchar(100) NULL,
    [TN04_RutaCorreoAprobacion] varchar(255) NULL,
    [TN04_RutaComprobantePago] varchar(255) NULL,
    [TN04_ValorPagado] decimal(18, 2) NULL,
    [TN04_FechaPago] datetime NULL,
    [TN04_Correo_Noti] varchar(150) NULL,
    [TN04_PaginaWeb] varchar(200) NULL,
    [TN04_RutaLogoEntidad] varchar(255) NULL,
    [TN04_TM01_CodigoSectorF] int NULL,
    [TN04_TM08_Consecutivo] int NULL,
    [TN04_RutaResumenPdf] varchar(255) NULL;

-- Elimina la columna temporal 'id' usada para la migración
-- *********************************************************************************************************    
ALTER TABLE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
DROP COLUMN id;

-- Eliminar el campo 'TN04_Codigo' que no es necesario
ALTER TABLE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
DROP COLUMN TN04_Codigo;


-- Script para renombrar la columna [TN07_TN04_Codigo] a [TN07_TM02_Codigo]
-- en la tabla [SIIR-ProdV1].[dbo].[TN07_Adjuntos].

USE [SIIR-ProdV1];
GO

EXEC sp_rename 
    @objname = '[dbo].[TN07_Adjuntos].[TN07_TN04_Codigo]', 
    @newname = 'TN07_TM02_Codigo', 
    @objtype = 'COLUMN';
GO

PRINT 'Columna [TN07_TN04_Codigo] renombrada a [TN07_TM02_Codigo] en la tabla [dbo].[TN07_Adjuntos]';
GO

-- Script para transferir tablas del esquema 'INSC' al esquema 'dbo'.
-- Se verifica si la tabla ya existe en 'dbo' antes de realizar la transferencia para evitar errores o reemplazos.

USE [SIIR-ProdV1]
GO

-- 1. TM00_ParametrosGenerales
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM00_ParametrosGenerales' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM00_ParametrosGenerales' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM00_ParametrosGenerales;
        PRINT 'Transferencia exitosa: INSC.TM00_ParametrosGenerales -> dbo.TM00_ParametrosGenerales';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM00_ParametrosGenerales ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM00_ParametrosGenerales no existe en el esquema INSC.';
END
GO

-- 2. TM01_Estado
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM01_Estado' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM01_Estado' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM01_Estado;
        PRINT 'Transferencia exitosa: INSC.TM01_Estado -> dbo.TM01_Estado';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM01_Estado ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM01_Estado no existe en el esquema INSC.';
END
GO

-- 3. TM01_SectorFinanciero
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM01_SectorFinanciero' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM01_SectorFinanciero' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM01_SectorFinanciero;
        PRINT 'Transferencia exitosa: INSC.TM01_SectorFinanciero -> dbo.TM01_SectorFinanciero';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM01_SectorFinanciero ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM01_SectorFinanciero no existe en el esquema INSC.';
END
GO

-- 4. TM02_Area
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM02_Area' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM02_Area' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM02_Area;
        PRINT 'Transferencia exitosa: INSC.TM02_Area -> dbo.TM02_Area';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM02_Area ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM02_Area no existe en el esquema INSC.';
END
GO

-- 5. TM02_ENTIDADFINANCIERA
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM02_ENTIDADFINANCIERA' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM02_ENTIDADFINANCIERA' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM02_ENTIDADFINANCIERA;
        PRINT 'Transferencia exitosa: INSC.TM02_ENTIDADFINANCIERA -> dbo.TM02_ENTIDADFINANCIERA';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM02_ENTIDADFINANCIERA ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM02_ENTIDADFINANCIERA no existe en el esquema INSC.';
END
GO


-- 6. TM03_Usuario
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM03_Usuario' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM03_Usuario' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM03_Usuario;
        PRINT 'Transferencia exitosa: INSC.TM03_Usuario -> dbo.TM03_Usuario';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM03_Usuario ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM03_Usuario no existe en el esquema INSC.';
END
GO

-- 7. TM07_Relacion_Estados
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM07_Relacion_Estados' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM07_Relacion_Estados' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM07_Relacion_Estados;
        PRINT 'Transferencia exitosa: INSC.TM07_Relacion_Estados -> dbo.TM07_Relacion_Estados';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM07_Relacion_Estados ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM07_Relacion_Estados no existe en el esquema INSC.';
END
GO

-- 8. TM08_ConsecutivoEnt
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TM08_ConsecutivoEnt' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TM08_ConsecutivoEnt' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TM08_ConsecutivoEnt;
        PRINT 'Transferencia exitosa: INSC.TM08_ConsecutivoEnt -> dbo.TM08_ConsecutivoEnt';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TM08_ConsecutivoEnt ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TM08_ConsecutivoEnt no existe en el esquema INSC.';
END
GO


-- 9. TN04_Entidad
-- IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TN04_Entidad' AND SCHEMA_ID('INSC') = schema_id)
-- BEGIN
--     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TN04_Entidad' AND SCHEMA_ID('dbo') = schema_id)
--     BEGIN
--         ALTER SCHEMA dbo TRANSFER INSC.TN04_Entidad;
--         PRINT 'Transferencia exitosa: INSC.TN04_Entidad -> dbo.TN04_Entidad';
--     END
--     ELSE
--     BEGIN
--         PRINT 'Omitido: dbo.TN04_Entidad ya existe.';
--     END
-- END
-- ELSE
-- BEGIN
--     PRINT 'Advertencia: INSC.TN04_Entidad no existe en el esquema INSC.';
-- END
-- GO

-- 10. TN05_Historico_Estado
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TN05_Historico_Estado' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TN05_Historico_Estado' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TN05_Historico_Estado;
        PRINT 'Transferencia exitosa: INSC.TN05_Historico_Estado -> dbo.TN05_Historico_Estado';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TN05_Historico_Estado ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TN05_Historico_Estado no existe en el esquema INSC.';
END
GO

-- 11. TN06_Pagos
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TN06_Pagos' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TN06_Pagos' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TN06_Pagos;
        PRINT 'Transferencia exitosa: INSC.TN06_Pagos -> dbo.TN06_Pagos';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TN06_Pagos ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TN06_Pagos no existe en el esquema INSC.';
END
GO

-- 12. TN07_Adjuntos
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TN07_Adjuntos' AND SCHEMA_ID('INSC') = schema_id)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TN07_Adjuntos' AND SCHEMA_ID('dbo') = schema_id)
    BEGIN
        ALTER SCHEMA dbo TRANSFER INSC.TN07_Adjuntos;
        PRINT 'Transferencia exitosa: INSC.TN07_Adjuntos -> dbo.TN07_Adjuntos';
    END
    ELSE
    BEGIN
        PRINT 'Omitido: dbo.TN07_Adjuntos ya existe.';
    END
END
ELSE
BEGIN
    PRINT 'Advertencia: INSC.TN07_Adjuntos no existe en el esquema INSC.';
END
GO


-- Script para eliminar directamente las tablas especificadas del esquema 'INSC'.
-- No se realiza validación de existencia en 'dbo' antes de la eliminación.
-- El proceso finaliza con la eliminación del esquema 'INSC' si está vacío.

DECLARE @TableName NVARCHAR(128);
DECLARE @SqlStatement NVARCHAR(500);
DECLARE @CursorTables CURSOR;

-- Definimos el cursor para solo las tres tablas solicitadas:
SET @CursorTables = CURSOR FOR
    SELECT name
    FROM sys.tables
    WHERE SCHEMA_ID('INSC') = schema_id
    AND name IN (
        'TM01_SectorFinanciero', 
        'TM02_ENTIDADFINANCIERA',
        'TN04_Entidad'
    );

OPEN @CursorTables;
FETCH NEXT FROM @CursorTables INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- 1. Eliminación directa de la tabla, verificando que exista en INSC por seguridad.
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = @TableName AND SCHEMA_ID('INSC') = schema_id)
    BEGIN
        SET @SqlStatement = N'DROP TABLE INSC.' + QUOTENAME(@TableName) + ';';
        EXEC sp_executesql @SqlStatement;
        PRINT 'DROP EXITOSO: Se eliminó INSC.' + @TableName + ' (eliminación directa solicitada).';
    END
    ELSE
    BEGIN
        PRINT 'ADVERTENCIA: La tabla INSC.' + @TableName + ' no se encontró para eliminar. Puede que ya haya sido transferida o eliminada.';
    END

    FETCH NEXT FROM @CursorTables INTO @TableName;
END

CLOSE @CursorTables;
DEALLOCATE @CursorTables;
GO

-- Paso Final: Eliminar el esquema INSC.
-- Esto solo funcionará si el esquema está completamente vacío de objetos (tablas, vistas, etc.)
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'INSC')
BEGIN
    -- Intentamos eliminar el esquema.
    DROP SCHEMA INSC;
    PRINT 'ÉXITO: El esquema INSC ha sido eliminado.';
END
ELSE
BEGIN
    PRINT 'El esquema INSC ya no existe.';
END
GO


-- Script para renombrar todas las columnas en [dbo].[TM02_ENTIDADFINANCIERA]
-- que comienzan con 'TN04_' para que comiencen con 'TM02_'.

USE [SIIR-ProdV1];
GO

DECLARE @OldColumnName NVARCHAR(128);
DECLARE @NewColumnName NVARCHAR(128);
DECLARE @ObjectName NVARCHAR(256) = '[dbo].[TM02_ENTIDADFINANCIERA]';
DECLARE @SqlStatement NVARCHAR(500);

-- Cursor para iterar sobre todas las columnas de la tabla que empiezan con 'TN04_'
DECLARE ColumnCursor CURSOR FOR
SELECT name
FROM sys.columns
WHERE object_id = OBJECT_ID(@ObjectName)
  -- La cláusula ESCAPE es necesaria para tratar el underscore (_) como literal y no como un comodín.
  AND name LIKE 'TN04\_%' ESCAPE '\';

OPEN ColumnCursor;
FETCH NEXT FROM ColumnCursor INTO @OldColumnName;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Construye el nuevo nombre de columna reemplazando 'TN04_' por 'TM02_'
    SET @NewColumnName = REPLACE(@OldColumnName, 'TN04_', 'TM02_');
    
    -- Construye la sentencia sp_rename para ejecutarla dinámicamente
    SET @SqlStatement = N'EXEC sp_rename @objname = ''' + @ObjectName + '.' + @OldColumnName + ''', @newname = ''' + @NewColumnName + ''', @objtype = ''COLUMN'';';
    
    -- Ejecuta el sp_rename
    EXEC sp_executesql @SqlStatement;
    
    PRINT 'Columna renombrada: ' + @OldColumnName + ' -> ' + @NewColumnName;

    FETCH NEXT FROM ColumnCursor INTO @OldColumnName;
END

CLOSE ColumnCursor;
DEALLOCATE ColumnCursor;

GO

PRINT 'Proceso de cambio de prefijos de columnas finalizado.';


-- Cambiar TN05_TN04_Tipo a TN05_TM02_Tipo
EXEC sp_rename '[SIIR-ProdV1].[dbo].[TN05_Historico_Estado].[TN05_TN04_Tipo]', 
               'TN05_TM02_Tipo', 
               'COLUMN';

-- Cambiar TN05_TN04_Codigo a TN05_TM02_Codigo
EXEC sp_rename '[SIIR-ProdV1].[dbo].[TN05_Historico_Estado].[TN05_TN04_Codigo]', 
               'TN05_TM02_Codigo', 
               'COLUMN';

-- Insertar el registro con la descripción "SIEF: Inscripción registrada. En validación de documentos."
INSERT INTO [SIIR-ProdV1].[dbo].[TM59_TIPOS_FORMATO] 
    ([TM59_Descripcion])
VALUES 
    ('SIEF: Inscripción registrada. En validación de documentos.');         

INSERT INTO [SIIR-ProdV1].[dbo].[TM59_TIPOS_FORMATO] 
    ([TM59_Descripcion])
VALUES 
    ('SIEF: En validación del pago.');     

INSERT INTO [SIIR-ProdV1].[dbo].[TM59_TIPOS_FORMATO] 
    ([TM59_Descripcion])
VALUES 
    ('SIEF: Pendiente de aprobación final.'); 

INSERT INTO [SIIR-ProdV1].[dbo].[TM59_TIPOS_FORMATO] 
    ([TM59_Descripcion])
VALUES 
    ('SIEF: Entidad inscrita.'); 

    -- Script para crear tabla TM80_LOG_CORREOS
-- Tabla para registrar el log de todos los correos enviados por el sistema

USE [SIIR-ProdV1]
GO

CREATE TABLE [dbo].[TM80_LOG_CORREOS] (
    [TM80_ID] INT IDENTITY(1,1) NOT NULL,
    [TM80_TM02_CODIGO] INT NULL,
    [TM80_NUMERO_TRAMITE] NVARCHAR(50) NULL,
    [TM80_DESTINATARIOS] NVARCHAR(MAX) NOT NULL,
    [TM80_ASUNTO] NVARCHAR(500) NOT NULL,
    [TM80_CUERPO] NVARCHAR(MAX) NULL,
    [TM80_TIPO_CORREO] NVARCHAR(50) NOT NULL,
    [TM80_ESTADO_ENVIO] NVARCHAR(20) NOT NULL,
    [TM80_FECHA_ENVIO] DATETIME2(7) NOT NULL,
    [TM80_USUARIO] NVARCHAR(100) NULL,
    [TM80_ERROR_DETALLE] NVARCHAR(MAX) NULL,
    
    CONSTRAINT [PK_TM80_LOG_CORREOS] PRIMARY KEY CLUSTERED ([TM80_ID] ASC)
);

-- Índices para mejorar rendimiento
CREATE INDEX [IX_TM80_TM02_CODIGO] ON [dbo].[TM80_LOG_CORREOS] ([TM80_TM02_CODIGO]);
CREATE INDEX [IX_TM80_FECHA_ENVIO] ON [dbo].[TM80_LOG_CORREOS] ([TM80_FECHA_ENVIO]);
CREATE INDEX [IX_TM80_ESTADO_ENVIO] ON [dbo].[TM80_LOG_CORREOS] ([TM80_ESTADO_ENVIO]);

-- Comentarios de la tabla
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Tabla para registrar el log de todos los correos enviados por el sistema SIEF',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'TM80_LOG_CORREOS';



USE [SistemasComunes]
GO

-- Script para insertar el nuevo registro en la tabla TM01_Aplicaciones
-- APLICACION DIF CON CODIGO 43

INSERT INTO [dbo].[TM01_Aplicaciones]
(
      [TM01_Codigo]
	, [TM01_Sigla]
    , [TM01_Nombre]
    , [TM01_Descripcion]
    , [TM01_CadenaDeConexion]
    , [TM01_RutaEjecutable]
    , [TM01_RutaIcono]
    , [TM01_SCA]
)
VALUES
(
	43
    ,  'DIF' -- TM01_Sigla
    , 'Departamento de Información Financiera' -- TM01_Nombre
    , 'Departamento de Información Financiera' -- TM01_Descripcion
    , 'N/A' -- TM01_CadenaDeConexion
    , NULL -- TM01_RutaEjecutable
    , NULL -- TM01_RutaIcono
    , 0    -- TM01_SCA
);

GO



-- Script para insertar el nuevo registro en la tabla TM02_Area PARA EL AREA DIF CON CODIGO 52060
USE [SIIR-ProdV1]
GO

INSERT INTO [dbo].[TM02_Area]
(
      [TM02_Codigo]
    , [TM02_Nombre]
)
VALUES
(
      52060 -- TM02_Codigo
    , 'DIF' -- TM02_Nombre
);

GO

-- Script para insertar el nuevo registro en la tabla TM02_Area PARA EL AREA DGC CON CODIGO 52070
USE [SIIR-ProdV1]
GO

INSERT INTO [dbo].[TM02_Area]
(
      [TM02_Codigo]
    , [TM02_Nombre]
)
VALUES
(
      52070
    , 'DGC'
);

GO


-- Script para insertar el nuevo registro en la tabla TM02_Area PARA EL AREA DOT CON CODIGO 52050
USE [SIIR-ProdV1]
GO

INSERT INTO [dbo].[TM02_Area]
(
      [TM02_Codigo]
    , [TM02_Nombre]
)
VALUES
(
      52050 
    , 'DOT'
);

GO


-- Cambiar el departamento para pruebas
USE [SistemasComunes]
GO

UPDATE [dbo].[TM04_Responsables]
SET 
    [TM04_TM03_Codigo] = '52060' 
WHERE 
    [TM04_Identificacion] = 'AlfredoMamby';
GO


-- Script para COPIAR el valor de TM02_TM01_CODIGO a TM02_TM01_CodigoSectorF 
-- en toda la tabla TM02_ENTIDADFINANCIERA.

USE [SIIR-ProdV1]
GO

UPDATE [dbo].[TM02_ENTIDADFINANCIERA]
SET 
    [TM02_TM01_CodigoSectorF] = [TM02_TM01_CODIGO];

GO

-- Se agrega un nuevo registro en TM02_Area para el area SMR con el c´ódigo 59010
INSERT INTO [SIIR-ProdV1].[dbo].[TM02_Area] ([TM02_Codigo], [TM02_Nombre])
VALUES ('52010', 'SMR');


-- Insertar un nuevo estado en la tabla TM01_Estado (En validación del pago)
INSERT INTO [dbo].[TM01_Estado]
(
    [TM01_TM01_Codigo],
    [TM01_Nombre],
    [TM01_TM02_Codigo]
)
VALUES
(
    12,
    'En validación del pago',
    52050
);

-- Insertar un nuevo estado en la tabla TM01_Estado (Pendiente de aprobación final)
INSERT INTO [dbo].[TM01_Estado]
(
    [TM01_TM01_Codigo],
    [TM01_Nombre],
    [TM01_TM02_Codigo]
)
VALUES
(
    13,
    'Pendiente de aprobación final',
    59030
);

-- Insertar un nuevo estado en la tabla TM01_Estado (Entidad inscrita)

  INSERT INTO [dbo].[TM01_Estado]
(
    [TM01_TM01_Codigo],
    [TM01_Nombre],
    [TM01_TM02_Codigo]
)
VALUES
(
    14,
    'Entidad inscrita',
    59030
);


  -- 1. Eliminar todos los registros de la tabla TM08_ConsecutivoEnt
-- TRUNCATE TABLE es más rápido y eficiente que DELETE FROM
-- para eliminar *todos* los registros de una tabla, y también
-- ayuda a resetear el ID autoincremental de algunas maneras.
TRUNCATE TABLE [SIIR-ProdV1].[dbo].[TM08_ConsecutivoEnt];

-- 2. Restablecer la semilla (el contador) del autoincremento
-- a un valor específico (normalmente 1).
-- El segundo parámetro es el valor *inicial* para el próximo ID.
DBCC CHECKIDENT ('[SIIR-ProdV1].[dbo].[TM08_ConsecutivoEnt]', RESEED, 1);


BEGIN TRANSACTION;

-- ***************************************************************
-- 1. Modificar el nombre del perfil 'Jefe DOT' a 'DOT'
--    También actualizamos la descripción para que sea la unificada y más completa.
-- ***************************************************************

UPDATE [SistemasComunes].[dbo].[TM14_PerfilesAplicacion]
SET
    [TM14_Perfil] = 'Profesional DOT',
    [TM14_Descripcion] = 'Perfil encargado de la aprobación de solicitudes, consulta de trámites, edición de información y cargue de documentos'
WHERE
    [TM14_TM01_Codigo] = '17'
    AND [TM14_Perfil] = 'Jefe DOT';

-- ***************************************************************
-- 2. Eliminar el perfil 'Profesional DOT'
-- ***************************************************************

DELETE FROM [SistemasComunes].[dbo].[TM14_PerfilesAplicacion]
WHERE
    [TM14_TM01_Codigo] = '17'
    AND [TM14_Perfil] = 'Profesional DOT';

-- ***************************************************************
-- 3. Confirmar la Transacción
-- ***************************************************************

-- Si estás seguro de que los pasos anteriores son correctos, descomenta la siguiente línea:
-- COMMIT TRANSACTION;

-- Si deseas revertir los cambios por algún error, usa la siguiente línea:
-- ROLLBACK TRANSACTION;


-- ACTUALIZA MANUALMENTE EL PERFIL DE UN 
-- ***************************************************************
UPDATE [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
SET TM15_TM14_Perfil = 'Profesional DOT'
WHERE TM15_TM04_Identificacion = 'AlfredoMamby' 
  AND TM15_TM12_TM01_Codigo = 17 
  AND TM15_TM12_Ambiente = 'PRODUCCION';


  -- Script para eliminar el Foreign Key constraint FK_TN05_Historico_Estado_TM03_Usuario
USE [SIIR-ProdV1]
GO

-- Eliminar el constraint de foreign key
ALTER TABLE [dbo].[TN05_Historico_Estado] 
DROP CONSTRAINT [FK_TN05_Historico_Estado_TM03_Usuario]
GO

PRINT 'Foreign Key constraint FK_TN05_Historico_Estado_TM03_Usuario eliminado exitosamente'



USE [SIIR-ProdV1];
GO

-- 1. Crear la tabla de la entidad Extracto de Pago
CREATE TABLE [dbo].[TN09_Extractos] (
    -- Clave Primaria
    [TN09_Id] INT IDENTITY(1,1) NOT NULL,

    -- Campos de la Modal
    [TN09_Fecha] DATE NOT NULL,
    [TN09_Valor] DECIMAL(19, 2) NOT NULL,

    -- Relación al Archivo Físico
    [TN09_TN07_Id] INT NOT NULL,

    -- Campos de Auditoría
    [TN09_FechaCarga] DATETIME NOT NULL DEFAULT GETDATE(),

    -- Restricciones
    CONSTRAINT PK_TN09_Extractos PRIMARY KEY CLUSTERED ([TN09_Id] ASC),

    -- Unicidad: Un archivo adjunto solo puede ser un extracto una vez
    CONSTRAINT UQ_TN09_TN07_Id UNIQUE ([TN09_TN07_Id]),

    -- Clave Foránea al Archivo Adjunto (TN07)
    CONSTRAINT FK_TN09_Extractos_TN07 FOREIGN KEY ([TN09_TN07_Id])
        REFERENCES [dbo].[TN07_Adjuntos] ([TN07_Id])
);
GO