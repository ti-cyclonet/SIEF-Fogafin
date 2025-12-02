# SIEF-Fogafin

Sistema de Inscripción de Entidades Financieras (SIEF) - Fogafin

## 🚀 Configuración para Despliegue

### Archivos de Configuración Requeridos

Antes del despliegue, configure los siguientes archivos:

1. **config/database.php** - Configuración de base de datos
2. **Back/InscripcionEntidades/local.settings.json** - Configuración Azure Functions
3. **front-interno/config.js** - Código de autorización Azure

### Variables de Entorno

- `SqlConnectionString`: Cadena de conexión a SQL Server
- `StorageConnectionString`: Cadena de conexión a Azure Storage
- `AZURE_FUNCTION_KEY`: Clave de autorización para Azure Functions

### Estructura del Proyecto

```
SIEF-Fogafin/
├── Back/                    # Azure Functions (.NET)
├── front-interno/           # Aplicación interna
├── front-publico/           # Aplicación pública
├── config/                  # Configuraciones PHP
├── DB/                      # Scripts de base de datos
└── api/                     # APIs PHP
```

## 🔐 Seguridad

- Todas las credenciales están en archivos .example
- Los archivos reales están en .gitignore
- Configure las variables de entorno en producción