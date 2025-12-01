# 🚀 GUÍA DE DESPLIEGUE SEGURO - SIEF-Fogafin

## ⚠️ ANTES DE HACER PUSH

### 1. Verificar archivos sensibles
```bash
# Verificar que no hay archivos sensibles
git status
git ls-files | findstr /i "local.settings config.js database.php .env"
```

### 2. Configurar credenciales como variables de entorno
- `local.settings.json` → Variables de entorno de Azure
- `config.js` → Variables de entorno del servidor web
- `database.php` → Variables de entorno del servidor PHP

## 🔧 CONFIGURACIÓN REQUERIDA

### Backend (Azure Functions)
```json
// En Azure Portal → Configuration → Application Settings
{
  "SqlConnectionString": "Server=...;Database=...;User Id=...;Password=...;",
  "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;",
  "CLOUD_AUTH_CODE": "tu_codigo_auth_aqui"
}
```

### Frontend
```javascript
// Usar variables de entorno o configuración del servidor
const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:7176/api';
const AUTH_CODE = process.env.AUTH_CODE || '';
```

## 📋 CHECKLIST PRE-DEPLOY

- [ ] Verificar .gitignore actualizado
- [ ] Confirmar que archivos sensibles no están en staging
- [ ] Variables de entorno configuradas en producción
- [ ] Archivos .example actualizados
- [ ] Tests pasando
- [ ] Build exitoso

## 🆘 EN CASO DE EMERGENCIA

### Si se perdieron cambios:
```bash
# Verificar reflog
git reflog

# Recuperar commit específico
git reset --hard HEAD@{n}

# O crear branch desde commit perdido
git checkout -b recovery HEAD@{n}
```

### Si se subieron credenciales por error:
```bash
# Remover del historial (PELIGROSO)
git filter-branch --force --index-filter 'git rm --cached --ignore-unmatch archivo_sensible' --prune-empty --tag-name-filter cat -- --all

# Forzar push (solo si es necesario)
git push origin --force --all
```