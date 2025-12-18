-- Script para insertar el parámetro de tasa de inscripción
-- Ejecutar en la base de datos SIIR-ProdV1

-- Verificar si ya existe el parámetro
IF NOT EXISTS (SELECT 1 FROM [SIIR-ProdV1].[dbo].[TM00_ParametrosGenerales] WHERE TM00_Descripcion = 'TASA_INSCRIPCION')
BEGIN
    INSERT INTO [SIIR-ProdV1].[dbo].[TM00_ParametrosGenerales] (TM00_Descripcion, TM00_Valor)
    VALUES ('TASA_INSCRIPCION', '0.000115')
    
    PRINT 'Parámetro TASA_INSCRIPCION insertado correctamente'
END
ELSE
BEGIN
    PRINT 'El parámetro TASA_INSCRIPCION ya existe'
END

-- Verificar el resultado
SELECT * FROM [SIIR-ProdV1].[dbo].[TM00_ParametrosGenerales] WHERE TM00_Descripcion = 'TASA_INSCRIPCION'