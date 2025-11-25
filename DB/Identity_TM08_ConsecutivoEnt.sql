-- 1. Crear tabla temporal con TM08_Consecutivo como Identity y Primary Key
CREATE TABLE [INSC].[TM08_ConsecutivoEnt_temp] (
    [TM08_Consecutivo] INT IDENTITY(1,1) PRIMARY KEY,
    [TM08_TM01_Codigo] INT NOT NULL,
    [TM08_Ano] INT NOT NULL
);

-- 2. Copiar datos sin incluir la columna Identity
INSERT INTO [INSC].[TM08_ConsecutivoEnt_temp] ([TM08_TM01_Codigo], [TM08_Ano])
SELECT [TM08_TM01_Codigo], [TM08_Ano]
FROM [INSC].[TM08_ConsecutivoEnt];

-- 3. Eliminar la tabla original
DROP TABLE [INSC].[TM08_ConsecutivoEnt];

-- 4. Renombrar la tabla temporal a la original
EXEC sp_rename '[INSC].[TM08_ConsecutivoEnt_temp]', 'TM08_ConsecutivoEnt';
