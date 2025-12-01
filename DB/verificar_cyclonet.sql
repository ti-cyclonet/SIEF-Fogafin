USE [SIIR-ProdV1];
GO

-- Verificar el estado de la entidad Cyclonet S. A. S.
SELECT 
    TM02_Codigo,
    TM02_Nombre,
    TM02_TM01_CodigoSectorF as SectorId,
    TM02_ACTIVO,
    CASE 
        WHEN TM02_ACTIVO = 1 THEN 'ACTIVO'
        WHEN TM02_ACTIVO = 0 THEN 'INACTIVO'
        WHEN TM02_ACTIVO IS NULL THEN 'NULL'
        ELSE 'OTRO VALOR'
    END as EstadoDescripcion
FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
WHERE TM02_Nombre LIKE '%Cyclonet%'
   OR TM02_Nombre LIKE '%CYCLONET%';

-- También verificar todas las entidades del sector 2 y su estado
SELECT 
    TM02_Codigo,
    TM02_Nombre,
    TM02_ACTIVO,
    CASE 
        WHEN TM02_ACTIVO = 1 THEN 'ACTIVO'
        WHEN TM02_ACTIVO = 0 THEN 'INACTIVO'
        WHEN TM02_ACTIVO IS NULL THEN 'NULL'
        ELSE 'OTRO VALOR'
    END as EstadoDescripcion
FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
WHERE TM02_TM01_CodigoSectorF = 2
ORDER BY TM02_ACTIVO DESC, TM02_Nombre;