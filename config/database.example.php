<?php
// Configuración de conexión a base de datos
$serverName = "TU_SERVIDOR_SQL";
$database = "TU_BASE_DATOS";
$username = "TU_USUARIO";
$password = "TU_PASSWORD";

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