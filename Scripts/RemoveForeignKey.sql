-- Script para eliminar el Foreign Key constraint FK_TN05_Historico_Estado_TM03_Usuario
USE [SIIR-ProdV1]
GO

-- Eliminar el constraint de foreign key
ALTER TABLE [dbo].[TN05_Historico_Estado] 
DROP CONSTRAINT [FK_TN05_Historico_Estado_TM03_Usuario]
GO

PRINT 'Foreign Key constraint FK_TN05_Historico_Estado_TM03_Usuario eliminado exitosamente'