<?php
header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, PUT, DELETE');
header('Access-Control-Allow-Headers: Content-Type');

require_once '../config/database.php';

$method = $_SERVER['REQUEST_METHOD'];
$input = json_decode(file_get_contents('php://input'), true);

switch($method) {
    case 'GET':
        obtenerUsuarios();
        break;
    case 'POST':
        crearUsuario($input);
        break;
    case 'PUT':
        actualizarUsuario($input);
        break;
    case 'DELETE':
        eliminarUsuario($input);
        break;
}

function obtenerUsuarios() {
    global $conn_sistemas;
    
    $sql = "SELECT * FROM [dbo].[VW_USUARIOS_SIEF] ORDER BY NombreCompleto";
    $stmt = sqlsrv_query($conn_sistemas, $sql);
    
    $usuarios = [];
    while($row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC)) {
        $usuarios[] = $row;
    }
    
    echo json_encode(['success' => true, 'data' => $usuarios]);
}

function crearUsuario($data) {
    global $conn_sistemas;
    
    // 1. Crear/actualizar en TM04_Responsables
    $sql = "INSERT INTO [dbo].[TM04_Responsables] 
            ([TM04_Identificacion], [TM04_Nombre], [TM04_Apellidos], [TM04_EMail], [TM04_TM03_Codigo], [TM04_Activo])
            VALUES (?, ?, ?, ?, ?, ?)";
    
    $nombres = explode(' ', $data['nombreCompleto'], 2);
    $nombre = $nombres[0];
    $apellidos = isset($nombres[1]) ? $nombres[1] : '';
    $activo = $data['estado'] === 'Activo' ? 1 : 0;
    
    $params = [$data['identificacion'], $nombre, $apellidos, $data['email'], $data['codigoArea'], $activo];
    $stmt = sqlsrv_query($conn_sistemas, $sql, $params);
    
    if($stmt) {
        // 2. Asignar a aplicación SIEF
        $sql2 = "INSERT INTO [dbo].[TM15_ConexionAppAmbXResponsable] 
                ([TM15_TM12_TM01_Codigo], [TM15_TM12_Ambiente], [TM15_TM14_Perfil], [TM15_TM04_Identificacion])
                VALUES (17, 'PROD', ?, ?)";
        
        $params2 = [$data['perfil'], $data['identificacion']];
        $stmt2 = sqlsrv_query($conn_sistemas, $sql2, $params2);
        
        if($stmt2) {
            echo json_encode(['success' => true, 'message' => 'Usuario creado exitosamente']);
        } else {
            echo json_encode(['success' => false, 'message' => 'Error al asignar perfil']);
        }
    } else {
        echo json_encode(['success' => false, 'message' => 'Error al crear usuario']);
    }
}

function actualizarUsuario($data) {
    global $conn_sistemas;
    
    // Actualizar TM04_Responsables
    $sql = "UPDATE [dbo].[TM04_Responsables] 
            SET [TM04_Nombre] = ?, [TM04_Apellidos] = ?, [TM04_EMail] = ?, 
                [TM04_TM03_Codigo] = ?, [TM04_Activo] = ?
            WHERE [TM04_Identificacion] = ?";
    
    $nombres = explode(' ', $data['nombreCompleto'], 2);
    $nombre = $nombres[0];
    $apellidos = isset($nombres[1]) ? $nombres[1] : '';
    $activo = $data['estado'] === 'Activo' ? 1 : 0;
    
    $params = [$nombre, $apellidos, $data['email'], $data['codigoArea'], $activo, $data['identificacion']];
    $stmt = sqlsrv_query($conn_sistemas, $sql, $params);
    
    if($stmt) {
        // Actualizar perfil
        $sql2 = "UPDATE [dbo].[TM15_ConexionAppAmbXResponsable] 
                SET [TM15_TM14_Perfil] = ?
                WHERE [TM15_TM04_Identificacion] = ? AND [TM15_TM12_TM01_Codigo] = 17";
        
        $params2 = [$data['perfil'], $data['identificacion']];
        sqlsrv_query($conn_sistemas, $sql2, $params2);
        
        echo json_encode(['success' => true, 'message' => 'Usuario actualizado exitosamente']);
    } else {
        echo json_encode(['success' => false, 'message' => 'Error al actualizar usuario']);
    }
}

function eliminarUsuario($data) {
    global $conn_sistemas;
    
    // Eliminar de TM15_ConexionAppAmbXResponsable
    $sql = "DELETE FROM [dbo].[TM15_ConexionAppAmbXResponsable] 
            WHERE [TM15_TM04_Identificacion] = ? AND [TM15_TM12_TM01_Codigo] = 17";
    
    $params = [$data['identificacion']];
    $stmt = sqlsrv_query($conn_sistemas, $sql, $params);
    
    if($stmt) {
        echo json_encode(['success' => true, 'message' => 'Usuario eliminado del sistema SIEF']);
    } else {
        echo json_encode(['success' => false, 'message' => 'Error al eliminar usuario']);
    }
}
?>