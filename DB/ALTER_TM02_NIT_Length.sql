-- Script para ampliar la longitud del campo TM02_NIT en la tabla TM02_ENTIDADFINANCIERA
-- Esto permite agregar 'R' al final del NIT sin perder información

USE [SIIR-ProdV1]
GO

-- Verificar la longitud actual del campo TM02_NIT
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TM02_ENTIDADFINANCIERA' 
AND COLUMN_NAME = 'TM02_NIT'
GO

-- Ampliar el campo TM02_NIT de VARCHAR(9) a VARCHAR(15) para permitir agregar sufijos
ALTER TABLE [dbo].[TM02_ENTIDADFINANCIERA]
ALTER COLUMN [TM02_NIT] VARCHAR(15) NOT NULL
GO

-- Verificar que el cambio se aplicó correctamente
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TM02_ENTIDADFINANCIERA' 
AND COLUMN_NAME = 'TM02_NIT'
GO

PRINT 'Campo TM02_NIT ampliado exitosamente a VARCHAR(15)'