<?php
// 🔧 CONFIGURACIÓN DE CONEXIÓN A BASE DE DATOS - PLANTILLA
// Copiar este archivo como database.php y configurar con valores reales

$serverName = getenv('DB_SERVER') ?: "TU_SERVIDOR_SQL";
$database = getenv('DB_NAME') ?: "TU_BASE_DATOS";
$username = getenv('DB_USER') ?: "TU_USUARIO";
$password = getenv('DB_PASSWORD') ?: "TU_PASSWORD";

$connectionInfo = array(
    "Database" => $database,
    "Uid" => $username,
    "PWD" => $password,
    "Encrypt" => true,
    "TrustServerCertificate" => false
);

$conn_sistemas = sqlsrv_connect($serverName, $connectionInfo);

if (!$conn_sistemas) {
    die("Error de conexión: " . print_r(sqlsrv_errors(), true));
}
?>