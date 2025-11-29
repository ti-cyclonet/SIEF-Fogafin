@echo off
echo 🚀 INICIANDO DESPLIEGUE SIEF-Fogafin...

REM Verificar archivos sensibles
echo 📋 Verificando archivos sensibles...
git ls-files | findstr /i "local.settings.json config.js database.php .env" > nul
if %errorlevel% equ 0 (
    echo ❌ ALERTA: Archivos sensibles detectados en Git
    git ls-files | findstr /i "local.settings.json config.js database.php .env"
    echo ⚠️  Revisar antes de continuar
    pause
)

REM Verificar estado de Git
echo 📊 Estado actual de Git:
git status --porcelain

REM Agregar cambios
echo ➕ Agregando cambios...
git add .

REM Commit con timestamp
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"
set "timestamp=%YYYY%-%MM%-%DD% %HH%:%Min%:%Sec%"

git commit -m "🚀 Deploy %timestamp%"

REM Push a origin
echo 🌐 Desplegando a repositorio...
git push origin master

if %errorlevel% equ 0 (
    echo ✅ DESPLIEGUE EXITOSO
) else (
    echo ❌ ERROR EN DESPLIEGUE
    echo 🔄 Intentando pull y merge...
    git pull origin master --no-edit
    git push origin master
)

echo 🎉 Despliegue completado
pause