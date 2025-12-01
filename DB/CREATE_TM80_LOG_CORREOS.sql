-- Script para crear tabla TM80_LOG_CORREOS
-- Tabla para registrar el log de todos los correos enviados por el sistema

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