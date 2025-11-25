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
            _logger.LogInformation("=== INICIANDO CONSULTA DE USUARIOS SIEF ===");

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
                            s.TM03_Codigo AS CodigoArea,
                            c.TM15_TM14_Perfil AS Perfil,
                            CASE WHEN r.TM04_Activo = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado,
                            p.TM14_Descripcion AS DescripcionPerfil
                        FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                        INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                        INNER JOIN [SistemasComunes].[dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
                        INNER JOIN [SistemasComunes].[dbo].[TM14_PerfilesAplicacion] p ON c.TM15_TM14_Perfil = p.TM14_Perfil AND p.TM14_TM01_Codigo = 17
                        WHERE c.TM15_TM12_TM01_Codigo = 17 AND c.TM15_TM12_Ambiente = 'PROD'
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
                                    CodigoArea = reader["CodigoArea"].ToString(),
                                    Perfil = reader["Perfil"].ToString(),
                                    Estado = reader["Estado"].ToString(),
                                    DescripcionPerfil = reader["DescripcionPerfil"].ToString()
                                });
                            }
                        }

                        // Agrupar por área y mostrar en consola
                        var usuariosPorArea = usuarios.GroupBy(u => ((dynamic)u).CodigoArea)
                            .OrderBy(g => g.Key);

                        _logger.LogInformation("📋 DESTINATARIOS AGRUPADOS POR ÁREA:");
                        foreach (var grupo in usuariosPorArea)
                        {
                            var primeraArea = grupo.First();
                            _logger.LogInformation($"\n🏢 ÁREA {((dynamic)primeraArea).CodigoArea}: {((dynamic)primeraArea).Area}");
                            
                            foreach (var usuario in grupo)
                            {
                                var u = (dynamic)usuario;
                                _logger.LogInformation($"  📧 {u.NombreCompleto} - {u.TM04_EMail}");
                            }
                        }

                        // Plantillas de correo
                        string representanteLegal = "Juan Pérez García";
                        string entidadNombre = "Banco Ejemplo S.A.";
                        string numeroTramiteStr = "123456789";
                        string linkConsulta = "https://sadevsiefexterno.z20.web.core.windows.net/pages/consulta.html";

                        _logger.LogInformation("\n📧 PLANTILLAS DE CORREO:");
                        
                        // Plantilla para área responsable
                        var plantillaArea = $@"
                        <p>Doctor(a) {representanteLegal},</p>
                        <p>La entidad <strong>{entidadNombre}</strong> ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín, con el número del trámite <strong>{numeroTramiteStr}</strong>.</p>
                        <p>Puede consultar el estado del trámite en el siguiente link: 
                           <a href='{linkConsulta}'>{linkConsulta}</a></p>
                        <p>Cordial saludo,<br/><br/>
 
                        Departamento de Sistema de Seguro de Depósitos<br/>
                        Fondo de Garantías de Instituciones Financieras – Fogafín<br/>
                        PBX: 601 4321370 extensiones 255 - 142</p>";

                        _logger.LogInformation($"\n🏢 PLANTILLA ÁREA RESPONSABLE:\n{plantillaArea}");

                        // Plantilla para usuario
                        var plantillaUsuario = $@"
                        <p>Estimado(a) {representanteLegal},</p>
                        <p>Gracias por registrar la entidad <strong>{entidadNombre}</strong> en el Sistema de Inscripción de Entidades Financieras (SIEF).</p>
                        <p>El trámite se ha registrado exitosamente con el número <strong>{numeroTramiteStr}</strong>.</p>
                        <p>Puede consultar su estado en el siguiente enlace:</p>
                        <p><a href='{linkConsulta}'>{linkConsulta}</a></p>
                        <p>Atentamente,<br/><strong>Equipo Fogafín</strong></p>";

                        _logger.LogInformation($"\n👤 PLANTILLA USUARIO:\n{plantillaUsuario}");
                        
                        _logger.LogInformation("=== FIN DE CONSULTA DE USUARIOS SIEF ===");

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(usuarios));
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
                                cmd.Parameters.AddWithValue("@nombre", nombre);
                                cmd.Parameters.AddWithValue("@apellidos", apellidos);
                                cmd.Parameters.AddWithValue("@email", email ?? "");
                                cmd.Parameters.AddWithValue("@codigoArea", codigoArea);
                                cmd.Parameters.AddWithValue("@activo", activo);
                                cmd.Parameters.AddWithValue("@identificacion", identificacion);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. Actualizar perfil
                            string updatePerfil = @"
                                UPDATE [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] 
                                SET [TM15_TM14_Perfil] = @perfil
                                WHERE [TM15_TM04_Identificacion] = @identificacion AND [TM15_TM12_TM01_Codigo] = 17";

                            using (var cmd = new SqlCommand(updatePerfil, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@perfil", perfil);
                                cmd.Parameters.AddWithValue("@identificacion", identificacion);
                                await cmd.ExecuteNonQueryAsync();
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
                        AND r.TM04_TM03_Codigo IN (59030, 52060, 52050, 52070, 59010)
                        AND NOT EXISTS (
                            SELECT 1 FROM [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c
                            WHERE c.TM15_TM04_Identificacion = r.TM04_Identificacion 
                            AND c.TM15_TM12_TM01_Codigo = 17
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
                "DIF" => "52060",
                "DOT" => "52050",
                "DGC" => "52070",
                "SMR" => "59010",
                _ => "59030"
            };
        }
    }
}