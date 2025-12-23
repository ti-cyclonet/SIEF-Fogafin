using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class RechazarInscripcion
    {
        private readonly ILogger _logger;

        public RechazarInscripcion(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<RechazarInscripcion>();
        }

        [Function("RechazarInscripcion")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Procesando rechazo de inscripción");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<RechazarInscripcionRequest>(requestBody);

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Obtener estado actual
                            string getEstadoActualQuery = @"
                                SELECT ISNULL(e.TM02_TM01_CODIGO, 0) FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] e
                                WHERE e.TM02_CODIGO = @entidadId";
                            
                            int estadoActual = 0;
                            using (var command = new SqlCommand(getEstadoActualQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                var result = await command.ExecuteScalarAsync();
                                estadoActual = result != null ? Convert.ToInt32(result) : 0;
                                _logger.LogInformation($"Estado actual obtenido: {estadoActual} para entidad {data.EntidadId}");
                            }

                            // 2. Eliminar registros de TM61_ENTIDADES_NOTIFICACION para evitar conflictos FK
                            string deleteNotificacionesQuery = @"
                                DELETE FROM [SIIR-ProdV1].[dbo].[TM61_ENTIDADES_NOTIFICACION] 
                                WHERE TM61_TM02_Codigo = @entidadId";

                            using (var command = new SqlCommand(deleteNotificacionesQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                await command.ExecuteNonQueryAsync();
                            }

                            // 2.1. Actualizar estado a "RECHAZADO SSD" (código 1) y agregar 'R' al NIT
                            string updateEntidadEstadoQuery = @"
                                UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] 
                                SET TM02_TM01_CODIGO = 1,
                                    TM02_NIT = TM02_NIT + 'R'
                                WHERE TM02_CODIGO = @entidadId AND TM02_NIT NOT LIKE '%R'";

                            using (var command = new SqlCommand(updateEntidadEstadoQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                int rowsAffected = await command.ExecuteNonQueryAsync();
                                _logger.LogInformation($"Entidad {data.EntidadId} actualizada. Filas afectadas: {rowsAffected}");
                            }

                            // 3. Insertar en histórico de estados
                            string insertHistoricoQuery = @"
                                INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado] 
                                (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                                VALUES (1, @entidadId, @estadoAnterior, 1, GETDATE(), @funcionario, @observaciones)";

                            using (var command = new SqlCommand(insertHistoricoQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                command.Parameters.AddWithValue("@estadoAnterior", estadoActual);
                                command.Parameters.AddWithValue("@funcionario", data.Funcionario);
                                command.Parameters.AddWithValue("@observaciones", data.Observaciones);
                                await command.ExecuteNonQueryAsync();
                            }

                            // 4. Obtener información para el correo
                            string getInfoCorreoQuery = @"
                                SELECT e.TM02_Nombre_Rep, e.TM02_Correo_Noti, 
                                       e.TM02_Correo_Rep, e.TM02_NombreResponsable, e.TM02_CorreoResponsable
                                FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] e
                                WHERE e.TM02_CODIGO = @entidadId";
                            
                            string nombreRepresentante = "";
                            List<string> destinatarios = new List<string>();
                            
                            using (var command = new SqlCommand(getInfoCorreoQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        nombreRepresentante = reader["TM02_Nombre_Rep"]?.ToString() ?? "";
                                        
                                        // Agregar correos de la entidad
                                        var correoNoti = reader["TM02_Correo_Noti"]?.ToString();
                                        var correoRep = reader["TM02_Correo_Rep"]?.ToString();
                                        var correoResp = reader["TM02_CorreoResponsable"]?.ToString();
                                        
                                        if (!string.IsNullOrEmpty(correoNoti)) destinatarios.Add(correoNoti);
                                        if (!string.IsNullOrEmpty(correoRep)) destinatarios.Add(correoRep);
                                        if (!string.IsNullOrEmpty(correoResp)) destinatarios.Add(correoResp);
                                    }
                                }
                            }
                            
                            // Obtener correos de usuarios habilitados en SIEF desde TM03_Usuario
                            string getUsuariosSiefQuery = @"
                                SELECT DISTINCT TM03_Correo
                                FROM [SIIR-ProdV1].[dbo].[TM03_Usuario]
                                WHERE TM03_Correo IS NOT NULL AND TM03_Correo != ''";
                            
                            using (var command = new SqlCommand(getUsuariosSiefQuery, connection, transaction))
                            {
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        var email = reader["TM03_Correo"]?.ToString();
                                        if (!string.IsNullOrEmpty(email))
                                        {
                                            destinatarios.Add(email);
                                        }
                                    }
                                }
                            }
                            
                            // Agregar correo institucional
                            destinatarios.Add("fogafin@fogafin.gov.co");
                            
                            // Filtrar duplicados
                            destinatarios = destinatarios.Distinct().ToList();
                            
                            // Crear plantilla de correo
                            string asunto = "Rechazo de Inscripción al Sistema de Seguro de Depósitos";
                            string cuerpoCorreo = $@"
Doctor(a) {nombreRepresentante}

El proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín ha sido rechazado por {data.Observaciones}.

Por favor valide la información ingresada anteriormente; de requerir continuar con el proceso de inscripción ante el Fondo, genere una nueva solicitud a través de nuestra página web.

Cordial saludo,

Departamento de Sistema de Seguro de Depósitos
Fondo de Garantías de Instituciones Financieras – Fogafín
PBX: 601 4321370 extensiones 255 - 142";
                            
                            // Filtrar para no enviar únicamente a fogafin@fogafin.gov.co
                            // var destinatariosParaEnvio = destinatarios.Where(d => !d.Contains("@fogafin.gov.co")).ToList();
                            var destinatariosParaEnvio = destinatarios.Where(d => d != "fogafin@fogafin.gov.co").ToList();
                            
                            // Log de destinatarios
                            _logger.LogInformation($"=== CORREO DE RECHAZO ===");
                            _logger.LogInformation($"Destinatarios para envío: {string.Join(", ", destinatariosParaEnvio)}");
                            _logger.LogInformation($"=========================");
                            
                            transaction.Commit();
                            
                            // Enviar correo de rechazo después del commit
                            if (destinatariosParaEnvio.Any())
                            {
                                bool correoEnviado = await EnviarCorreoRechazoAsync(destinatariosParaEnvio, asunto, cuerpoCorreo, data.EntidadId);
                                _logger.LogInformation($"Correo de rechazo enviado: {correoEnviado}");
                            }

                            var response = req.CreateResponse(HttpStatusCode.OK);
                            response.Headers.Add("Content-Type", "application/json");
                            await response.WriteStringAsync(JsonSerializer.Serialize(new { mensaje = "Inscripción rechazada exitosamente" }));
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
                _logger.LogError(ex, "Error al rechazar inscripción");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }

        private async Task<bool> EnviarCorreoRechazoAsync(List<string> destinatarios, string asunto, string cuerpo, int entidadId)
        {
            try
            {
                string baseUrl = Environment.GetEnvironmentVariable("CENTRALIZED_EMAIL_URL") ?? "https://app-correo-centralizado-dev-bfbrcqgdgfbbaxhq.eastus2-01.azurewebsites.net";
                string apiKey = Environment.GetEnvironmentVariable("CENTRALIZED_EMAIL_API_KEY") ?? "envioCorreo-2025";

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var emailBody = new
                {
                    to = destinatarios,
                    subject = asunto,
                    htmlBody = $"<p>{cuerpo.Replace("\n", "<br>")}</p>",
                    textBody = cuerpo,
                    priority = "normal"
                };

                string jsonBody = System.Text.Json.JsonSerializer.Serialize(emailBody);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("/api/email/send", content);
                bool exitoso = response.IsSuccessStatusCode;

                // Registrar en log
                await RegistrarLogCorreoAsync(entidadId, destinatarios, asunto, cuerpo, exitoso, response.ReasonPhrase);

                return exitoso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de rechazo");
                await RegistrarLogCorreoAsync(entidadId, destinatarios, asunto, cuerpo, false, ex.Message);
                return false;
            }
        }

        private async Task RegistrarLogCorreoAsync(int entidadId, List<string> destinatarios, string asunto, string cuerpo, bool exitoso, string? error)
        {
            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string insertLogQuery = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM80_LOG_CORREOS]
                    (TM80_TM02_CODIGO, TM80_NUMERO_TRAMITE, TM80_DESTINATARIOS, TM80_ASUNTO, TM80_CUERPO, TM80_TIPO_CORREO, TM80_ESTADO_ENVIO, TM80_FECHA_ENVIO, TM80_USUARIO, TM80_ERROR_DETALLE)
                    VALUES (@entidadId, '', @destinatarios, @asunto, @cuerpo, 'RECHAZO_INSCRIPCION', @estado, GETDATE(), 'USUARIOWEB', @error)";

                using var command = new SqlCommand(insertLogQuery, connection);
                command.Parameters.AddWithValue("@entidadId", entidadId);
                command.Parameters.AddWithValue("@destinatarios", string.Join(";", destinatarios));
                command.Parameters.AddWithValue("@asunto", asunto);
                command.Parameters.AddWithValue("@cuerpo", cuerpo.Length > 4000 ? cuerpo.Substring(0, 4000) : cuerpo);
                command.Parameters.AddWithValue("@estado", exitoso ? "ENVIADO" : "ERROR");
                command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar log de correo de rechazo");
            }
        }
    }

    public class RechazarInscripcionRequest
    {
        public int EntidadId { get; set; }
        public string Observaciones { get; set; }
        public string Funcionario { get; set; }
    }
}