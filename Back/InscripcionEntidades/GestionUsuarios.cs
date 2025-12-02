using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class GestionUsuarios
    {
        private readonly ILogger _logger;

        public GestionUsuarios(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GestionUsuarios>();
        }

        [Function("ConsultarUsuarios")]
        public async Task<HttpResponseData> ConsultarUsuarios(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usuarios")] HttpRequestData req)
        {


            try
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                int page = int.TryParse(queryParams["page"], out int p) ? p : 1;
                int pageSize = 8;
                int offset = (page - 1) * pageSize;

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string countQuery = @"
                        SELECT COUNT(*)
                        FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                        INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                        WHERE c.TM15_TM12_TM01_Codigo = 17 AND c.TM15_TM12_Ambiente IN ('PROD', 'PRODUCCION', 'PRUEBAS')";

                    int totalRecords;
                    using (var countCommand = new SqlCommand(countQuery, connection))
                    {
                        countCommand.CommandTimeout = 60;
                        totalRecords = (int)await countCommand.ExecuteScalarAsync();
                    }

                    string query = @"
                        SELECT 
                            r.TM04_Identificacion,
                            r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
                            r.TM04_EMail,
                            s.TM03_Nombre AS Area,
                            s.TM03_Codigo AS CodigoArea,
                            c.TM15_TM14_Perfil AS Perfil,
                            CASE WHEN r.TM04_Activo = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado,
                            c.TM15_TM14_Perfil AS DescripcionPerfil
                        FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                        INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                        INNER JOIN [SistemasComunes].[dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
                        WHERE c.TM15_TM12_TM01_Codigo = 17 AND c.TM15_TM12_Ambiente IN ('PROD', 'PRODUCCION', 'PRUEBAS')
                        ORDER BY r.TM04_Nombre, r.TM04_Apellidos
                        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 60; // 60 segundos timeout
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PageSize", pageSize);
                        
                        var usuarios = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                usuarios.Add(new
                                {
                                    TM04_Identificacion = reader["TM04_Identificacion"].ToString(),
                                    NombreCompleto = reader["NombreCompleto"].ToString(),
                                    TM04_EMail = reader["TM04_EMail"].ToString(),
                                    Area = reader["Area"].ToString(),
                                    CodigoArea = reader["CodigoArea"].ToString(),
                                    Perfil = reader["Perfil"].ToString(),
                                    Estado = reader["Estado"].ToString(),
                                    DescripcionPerfil = reader["DescripcionPerfil"].ToString()
                                });
                            }
                        }

                        var result = new
                        {
                            usuarios = usuarios,
                            totalRecords = totalRecords,
                            currentPage = page,
                            pageSize = pageSize,
                            totalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
                        };

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(result));
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar usuarios");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }

        [Function("CrearUsuario")]
        public async Task<HttpResponseData> CrearUsuario(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "usuarios")] HttpRequestData req)
        {
            _logger.LogInformation("Activando usuario para SIEF");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

                string identificacion = data["identificacion"].ToString();
                string perfil = data["perfil"].ToString();

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    // 1. Verificar si el usuario existe en TM04_Responsables
                    string checkUser = "SELECT COUNT(*) FROM [SistemasComunes].[dbo].[TM04_Responsables] WHERE [TM04_Identificacion] = @identificacion";
                    
                    using (var cmd = new SqlCommand(checkUser, connection))
                    {
                        cmd.Parameters.AddWithValue("@identificacion", identificacion);
                        int userExists = (int)await cmd.ExecuteScalarAsync();
                        
                        if (userExists == 0)
                        {
                            var response = req.CreateResponse(HttpStatusCode.BadRequest);
                            response.Headers.Add("Content-Type", "application/json");
                            var error = new { success = false, message = "Usuario no encontrado en el sistema. Debe ser creado primero en TM04_Responsables." };
                            await response.WriteStringAsync(JsonSerializer.Serialize(error));
                            return response;
                        }
                    }
                    
                    // 2. Verificar si ya tiene acceso a SIEF
                    string checkSief = @"SELECT COUNT(*) FROM [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                                        WHERE [TM15_TM04_Identificacion] = @identificacion AND [TM15_TM12_TM01_Codigo] = 17";
                    
                    using (var cmd = new SqlCommand(checkSief, connection))
                    {
                        cmd.Parameters.AddWithValue("@identificacion", identificacion);
                        int siefAccess = (int)await cmd.ExecuteScalarAsync();
                        
                        if (siefAccess > 0)
                        {
                            var response = req.CreateResponse(HttpStatusCode.BadRequest);
                            response.Headers.Add("Content-Type", "application/json");
                            var error = new { success = false, message = "El usuario ya tiene acceso al sistema SIEF." };
                            await response.WriteStringAsync(JsonSerializer.Serialize(error));
                            return response;
                        }
                    }
                    
                    // 3. Asignar acceso a SIEF
                    string insertConexion = @"
                        INSERT INTO [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                        ([TM15_TM12_TM01_Codigo], [TM15_TM12_Ambiente], [TM15_TM14_Perfil], [TM15_TM04_Identificacion])
                        VALUES (17, 'PROD', @perfil, @identificacion)";

                    using (var cmd = new SqlCommand(insertConexion, connection))
                    {
                        cmd.Parameters.AddWithValue("@perfil", perfil);
                        cmd.Parameters.AddWithValue("@identificacion", identificacion);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    var successResponse = req.CreateResponse(HttpStatusCode.OK);
                    successResponse.Headers.Add("Content-Type", "application/json");
                    var resultado = new { success = true, message = "Usuario activado para SIEF exitosamente" };
                    await successResponse.WriteStringAsync(JsonSerializer.Serialize(resultado));
                    return successResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar usuario para SIEF");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                var error = new { success = false, message = "Error al activar usuario: " + ex.Message };
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(error));
                return errorResponse;
            }
        }

        [Function("ActualizarUsuario")]
        public async Task<HttpResponseData> ActualizarUsuario(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "usuarios/{identificacion}")] HttpRequestData req,
            string identificacion)
        {
            _logger.LogInformation($"Actualizando usuario: {identificacion}");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

                string nombreCompleto = data["nombreCompleto"].ToString();
                string email = data["email"].ToString();
                string area = data["area"].ToString();
                string perfil = data["perfil"].ToString();
                string estado = data["estado"].ToString();
                
                if (!IsValidPerfil(perfil))
                {
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    response.Headers.Add("Content-Type", "application/json");
                    var error = new { success = false, message = $"Perfil '{perfil}' no válido. Perfiles disponibles: Consulta, DOT, Jefe SSD, Profesional SSD" };
                    await response.WriteStringAsync(JsonSerializer.Serialize(error));
                    return response;
                }

                var nombres = nombreCompleto.Split(' ', 2);
                string nombre = nombres[0];
                string apellidos = nombres.Length > 1 ? nombres[1] : "";
                int activo = estado == "Activo" ? 1 : 0;
                string codigoArea = GetCodigoArea(area);

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Actualizar TM04_Responsables
                            string updateResponsable = @"
                                UPDATE [SistemasComunes].[dbo].[TM04_Responsables] 
                                SET [TM04_Nombre] = @nombre, [TM04_Apellidos] = @apellidos, 
                                    [TM04_EMail] = @email, [TM04_TM03_Codigo] = @codigoArea, [TM04_Activo] = @activo
                                WHERE [TM04_Identificacion] = @identificacion";

                            using (var cmd = new SqlCommand(updateResponsable, connection, transaction))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@nombre", nombre);
                                cmd.Parameters.AddWithValue("@apellidos", apellidos);
                                cmd.Parameters.AddWithValue("@email", email ?? "");
                                cmd.Parameters.AddWithValue("@codigoArea", codigoArea);
                                cmd.Parameters.AddWithValue("@activo", activo);
                                cmd.Parameters.AddWithValue("@identificacion", identificacion);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. Verificar si existe acceso SIEF y crear/actualizar
                            string checkSief = @"SELECT COUNT(*) FROM [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                                                WHERE [TM15_TM04_Identificacion] = @identificacion AND [TM15_TM12_TM01_Codigo] = 17";
                            
                            int siefExists;
                            using (var checkCmd = new SqlCommand(checkSief, connection, transaction))
                            {
                                checkCmd.Parameters.AddWithValue("@identificacion", identificacion);
                                siefExists = (int)await checkCmd.ExecuteScalarAsync();
                            }
                            
                            if (siefExists > 0)
                            {
                                // Actualizar perfil existente
                                string updatePerfil = @"
                                    UPDATE [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                                    SET [TM15_TM14_Perfil] = @perfil
                                    WHERE [TM15_TM04_Identificacion] = @identificacion 
                                    AND [TM15_TM12_TM01_Codigo] = 17 
                                    AND [TM15_TM12_Ambiente] IN ('PROD', 'PRODUCCION', 'PRUEBAS')";

                                using (var cmd = new SqlCommand(updatePerfil, connection, transaction))
                                {
                                    cmd.CommandTimeout = 120;
                                    cmd.Parameters.AddWithValue("@perfil", perfil);
                                    cmd.Parameters.AddWithValue("@identificacion", identificacion);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // Crear nuevo acceso SIEF
                                string insertConexion = @"
                                    INSERT INTO [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                                    ([TM15_TM12_TM01_Codigo], [TM15_TM12_Ambiente], [TM15_TM14_Perfil], [TM15_TM04_Identificacion])
                                    VALUES (17, 'PROD', @perfil, @identificacion)";

                                using (var cmd = new SqlCommand(insertConexion, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@perfil", perfil);
                                    cmd.Parameters.AddWithValue("@identificacion", identificacion);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();

                            var response = req.CreateResponse(HttpStatusCode.OK);
                            response.Headers.Add("Content-Type", "application/json");
                            var resultado = new { success = true, message = "Usuario actualizado exitosamente" };
                            await response.WriteStringAsync(JsonSerializer.Serialize(resultado));
                            return response;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                var error = new { success = false, message = "Error al actualizar usuario: " + ex.Message };
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(error));
                return errorResponse;
            }
        }

        [Function("EliminarUsuario")]
        public async Task<HttpResponseData> EliminarUsuario(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "usuarios/{identificacion}")] HttpRequestData req,
            string identificacion)
        {
            _logger.LogInformation($"Eliminando usuario del sistema SIEF: {identificacion}");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string deleteQuery = @"
                        DELETE FROM [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                        WHERE [TM15_TM04_Identificacion] = @identificacion AND [TM15_TM12_TM01_Codigo] = 17";

                    using (var command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@identificacion", identificacion);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        
                        if (rowsAffected > 0)
                        {
                            var resultado = new { success = true, message = "Usuario eliminado del sistema SIEF exitosamente" };
                            await response.WriteStringAsync(JsonSerializer.Serialize(resultado));
                        }
                        else
                        {
                            var resultado = new { success = false, message = "Usuario no encontrado en el sistema SIEF" };
                            await response.WriteStringAsync(JsonSerializer.Serialize(resultado));
                        }
                        
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                var error = new { success = false, message = "Error al eliminar usuario: " + ex.Message };
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(error));
                return errorResponse;
            }
        }

        [Function("ConsultarUsuariosDisponibles")]
        public async Task<HttpResponseData> ConsultarUsuariosDisponibles(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usuarios-disponibles")] HttpRequestData req)
        {
            _logger.LogInformation("Consultando usuarios disponibles para activar en SIEF");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            r.TM04_Identificacion,
                            r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
                            r.TM04_EMail,
                            s.TM03_Nombre AS Area,
                            s.TM03_Codigo AS CodigoArea
                        FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                        INNER JOIN [SistemasComunes].[dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
                        WHERE r.TM04_Activo = 1
                        AND NOT EXISTS (
                            SELECT 1 FROM [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c
                            WHERE c.TM15_TM04_Identificacion = r.TM04_Identificacion 
                            AND c.TM15_TM12_TM01_Codigo = 17
                            AND c.TM15_TM12_Ambiente = 'PROD'
                        )
                        ORDER BY r.TM04_Nombre, r.TM04_Apellidos";

                    using (var command = new SqlCommand(query, connection))
                    {
                        var usuarios = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                usuarios.Add(new
                                {
                                    TM04_Identificacion = reader["TM04_Identificacion"].ToString(),
                                    NombreCompleto = reader["NombreCompleto"].ToString(),
                                    TM04_EMail = reader["TM04_EMail"].ToString(),
                                    Area = reader["Area"].ToString(),
                                    CodigoArea = reader["CodigoArea"].ToString()
                                });
                            }
                        }

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(usuarios));
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar usuarios disponibles");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }

        private string GetCodigoArea(string area)
        {
            return area switch
            {
                "SSD" => "59030",
                "DEPARTAMENTO DEL SISTEMA DE SEGURO DE DEPOSITOS" => "59030",
                "DEPARTAMENTO DE OPERACIONES DE TESORERÍA" => "52050",
                "DIF" => "52060", 
                "DOT" => "52050",
                "DGC" => "52070",
                "SMR" => "52010",
                "DJU" => "51020",
                _ => "59030"
            };
        }
        
        [Function("ConsultarNotificaciones")]
        public async Task<HttpResponseData> ConsultarNotificaciones(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usuarios/notificaciones")] HttpRequestData req)
        {
            _logger.LogInformation("Consultando usuarios con notificaciones activas");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            TM03_Usuario,
                            TM03_TM02_Codigo,
                            TM03_Nombre,
                            TM03_Correo
                        FROM [SIIR-ProdV1].[dbo].[TM03_Usuario]";

                    using (var command = new SqlCommand(query, connection))
                    {
                        var usuarios = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                usuarios.Add(new
                                {
                                    TM03_Usuario = reader["TM03_Usuario"].ToString(),
                                    TM03_TM02_Codigo = reader["TM03_TM02_Codigo"].ToString(),
                                    TM03_Nombre = reader["TM03_Nombre"].ToString(),
                                    TM03_Correo = reader["TM03_Correo"].ToString()
                                });
                            }
                        }

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(usuarios));
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar notificaciones");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }

        [Function("GestionarNotificaciones")]
        public async Task<HttpResponseData> GestionarNotificaciones(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "usuarios/notificaciones")] HttpRequestData req)
        {
            _logger.LogInformation("Gestionando notificaciones de usuario");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

                string usuario = data["TM03_Usuario"].ToString();
                string codigoArea = data["TM03_TM02_Codigo"].ToString();
                string nombre = data["TM03_Nombre"].ToString();
                string correo = data["TM03_Correo"].ToString();
                bool activarNotificaciones = bool.Parse(data["activarNotificaciones"].ToString());

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    if (activarNotificaciones)
                    {
                        // Verificar si ya existe
                        string checkQuery = "SELECT COUNT(*) FROM [SIIR-ProdV1].[dbo].[TM03_Usuario] WHERE [TM03_Usuario] = @usuario";
                        using (var checkCmd = new SqlCommand(checkQuery, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@usuario", usuario);
                            int exists = (int)await checkCmd.ExecuteScalarAsync();

                            if (exists == 0)
                            {
                                // Insertar nuevo registro
                                string insertQuery = @"
                                    INSERT INTO [SIIR-ProdV1].[dbo].[TM03_Usuario] 
                                    ([TM03_Usuario], [TM03_TM02_Codigo], [TM03_Nombre], [TM03_Correo])
                                    VALUES (@usuario, @codigoArea, @nombre, @correo)";

                                using (var insertCmd = new SqlCommand(insertQuery, connection))
                                {
                                    insertCmd.Parameters.AddWithValue("@usuario", usuario);
                                    insertCmd.Parameters.AddWithValue("@codigoArea", codigoArea);
                                    insertCmd.Parameters.AddWithValue("@nombre", nombre);
                                    insertCmd.Parameters.AddWithValue("@correo", correo);
                                    await insertCmd.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // Actualizar registro existente
                                string updateQuery = @"
                                    UPDATE [SIIR-ProdV1].[dbo].[TM03_Usuario] 
                                    SET [TM03_TM02_Codigo] = @codigoArea, [TM03_Nombre] = @nombre, [TM03_Correo] = @correo
                                    WHERE [TM03_Usuario] = @usuario";

                                using (var updateCmd = new SqlCommand(updateQuery, connection))
                                {
                                    updateCmd.Parameters.AddWithValue("@codigoArea", codigoArea);
                                    updateCmd.Parameters.AddWithValue("@nombre", nombre);
                                    updateCmd.Parameters.AddWithValue("@correo", correo);
                                    updateCmd.Parameters.AddWithValue("@usuario", usuario);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }
                    else
                    {
                        // Eliminar registro de notificaciones
                        string deleteQuery = "DELETE FROM [SIIR-ProdV1].[dbo].[TM03_Usuario] WHERE [TM03_Usuario] = @usuario";
                        using (var deleteCmd = new SqlCommand(deleteQuery, connection))
                        {
                            deleteCmd.Parameters.AddWithValue("@usuario", usuario);
                            await deleteCmd.ExecuteNonQueryAsync();
                        }
                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    var resultado = new { 
                        success = true, 
                        message = activarNotificaciones ? "Usuario activado para notificaciones" : "Usuario desactivado de notificaciones" 
                    };
                    await response.WriteStringAsync(JsonSerializer.Serialize(resultado));
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al gestionar notificaciones");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                var error = new { success = false, message = "Error al gestionar notificaciones: " + ex.Message };
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(error));
                return errorResponse;
            }
        }

        private bool IsValidPerfil(string perfil)
        {
            var validPerfiles = new[] { "Consulta", "Profesional DOT", "Jefe SSD", "Profesional SSD", "Jefe / Profesional DOT" };
            return validPerfiles.Contains(perfil);
        }
    }
}