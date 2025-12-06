using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using Newtonsoft.Json;

namespace InscripcionEntidades
{
    public class AprobarDocumentos
    {
        private readonly ILogger _logger;

        public AprobarDocumentos(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AprobarDocumentos>();
        }

        [Function("AprobarDocumentos")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "AprobarDocumentos")] HttpRequestData req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                using (JsonDocument doc = JsonDocument.Parse(requestBody))
                {
                    JsonElement root = doc.RootElement;
                    
                    int entidadId = root.TryGetProperty("entidadId", out var entidadIdProp) ? entidadIdProp.GetInt32() : 0;
                    string observaciones = root.TryGetProperty("observaciones", out var observacionesProp) ? observacionesProp.GetString() ?? "" : "";
                    string usuario = root.TryGetProperty("usuario", out var usuarioProp) ? usuarioProp.GetString() ?? "" : "";

                    string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        // Obtener RazonSocial, estado anterior y número de trámite
                        string getInfoQuery = @"
                            SELECT e.TM02_NOMBRE, e.TM02_TM01_CODIGO, 
                                   CONCAT(c.TM08_Consecutivo, c.TM08_TM01_Codigo, c.TM08_Ano) AS NumeroTramite
                            FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] e
                            LEFT JOIN [SIIR-ProdV1].[dbo].[TM08_ConsecutivoEnt] c ON e.TM02_TM08_Consecutivo = c.TM08_Consecutivo
                            WHERE e.TM02_CODIGO = @entidadId";
                        string razonSocial = "";
                        string numeroTramite = "";
                        int estadoAnterior = 0;

                        using (var command = new SqlCommand(getInfoQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    razonSocial = reader["TM02_NOMBRE"]?.ToString() ?? "";
                                    numeroTramite = reader["NumeroTramite"]?.ToString() ?? "";
                                    estadoAnterior = reader["TM02_TM01_CODIGO"] != DBNull.Value ? Convert.ToInt32(reader["TM02_TM01_CODIGO"]) : 0;
                                }
                            }
                        }

                        List<string> destinatarios = new();
                        string correosDOTQuery = @"
                            SELECT DISTINCT TM03_Correo
                            FROM [SIIR-ProdV1].[dbo].[TM03_Usuario]
                            WHERE TM03_TM02_Codigo = '52050' 
                            AND TM03_Correo IS NOT NULL 
                            AND TM03_Correo != ''";

                        using (var command = new SqlCommand(correosDOTQuery, connection))
                        {
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    if (!reader.IsDBNull(0))
                                    {
                                        string email = reader.GetString(0);
                                        if (!string.IsNullOrWhiteSpace(email))
                                        {
                                            destinatarios.Add(email);
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (!destinatarios.Contains("fogafin@fogafin.gov.co"))
                        {
                            destinatarios.Add("fogafin@fogafin.gov.co");
                        }

                        string asunto = "Proceso de Inscripción al Sistema de Seguro de Depósitos";
                        string cuerpo = $@"Departamento de Operaciones de Tesorería:

La entidad {razonSocial} ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín, con el código de trámite No. {numeroTramite}.

Le solicitamos iniciar el proceso de validación de la información del pago en los aplicativos correspondientes y actualizar el estado del proceso en el aplicativo de Inscripción de entidades

Cordial saludo,

Departamento de Sistema de Seguro de Depósitos
Fondo de Garantías de Instituciones Financieras – Fogafín
PBX: 601 4321370 extensiones 255 - 142";

                        // Filtrar correos para no enviar temporalmente a @fogafin.gov.co
                        var destinatariosFiltrados = destinatarios.Where(email => !email.EndsWith("@fogafin.gov.co")).ToList();
                        
                        _logger.LogInformation("=== NOTIFICACIÓN DE CORREO ===");
                        _logger.LogInformation($"Destinatarios originales: {string.Join(", ", destinatarios)}");
                        _logger.LogInformation($"Destinatarios filtrados: {string.Join(", ", destinatariosFiltrados)}");
                        _logger.LogInformation($"Asunto: {asunto}");
                        _logger.LogInformation($"Cuerpo del mensaje:\n{cuerpo}");
                        _logger.LogInformation("==============================");

                        // Enviar correo usando microservicio centralizado
                        if (destinatariosFiltrados.Any())
                        {
                            bool correoEnviado = await EnviarCorreoAsync(destinatariosFiltrados, asunto, cuerpo, entidadId, numeroTramite);
                            _logger.LogInformation($"Correo enviado: {correoEnviado}");
                        }

                        // Eliminar registros de TM61_ENTIDADES_NOTIFICACION para evitar conflictos FK
                        string deleteNotificacionesQuery = @"
                            DELETE FROM [SIIR-ProdV1].[dbo].[TM61_ENTIDADES_NOTIFICACION] 
                            WHERE TM61_TM02_Codigo = @entidadId";

                        using (var command = new SqlCommand(deleteNotificacionesQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Validar usuario
                        string usuarioFinal = usuario.ToLower() == "adminsief" ? "USUARIOWEB" : usuario;

                        // Cambiar estado a "En validación del pago" (código 13)
                        string updateQuery = @"
                            UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
                            SET TM02_TM01_CODIGO = 13
                            WHERE TM02_CODIGO = @entidadId";

                        using (var command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Siempre registrar en histórico de estados
                        string observacionesCompletas = string.IsNullOrWhiteSpace(observaciones) 
                            ? "Aprobación de documentos - Cambio a validación de pago" 
                            : $"Aprobación de documentos: {observaciones}";
                            
                        string insertHistoricoQuery = @"
                            INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado]
                            (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                            VALUES (1, @entidadId, @estadoAnterior, 13, GETDATE(), @usuario, @observaciones)";

                        using (var command2 = new SqlCommand(insertHistoricoQuery, connection))
                        {
                            command2.Parameters.AddWithValue("@entidadId", entidadId);
                            command2.Parameters.AddWithValue("@estadoAnterior", estadoAnterior);
                            command2.Parameters.AddWithValue("@usuario", usuarioFinal);
                            command2.Parameters.AddWithValue("@observaciones", observacionesCompletas);
                            await command2.ExecuteNonQueryAsync();
                        }


                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(new { success = true, message = "Documentos aprobados correctamente" }));
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aprobar documentos");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }

        private async Task<bool> EnviarCorreoAsync(List<string> destinatarios, string asunto, string cuerpo, int entidadId, string numeroTramite)
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

                string jsonBody = JsonConvert.SerializeObject(emailBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("/api/email/send", content);
                bool exitoso = response.IsSuccessStatusCode;

                // Registrar en log
                await RegistrarLogCorreoAsync(entidadId, numeroTramite, destinatarios, asunto, cuerpo, exitoso, response.ReasonPhrase);

                return exitoso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo");
                await RegistrarLogCorreoAsync(entidadId, numeroTramite, destinatarios, asunto, cuerpo, false, ex.Message);
                return false;
            }
        }

        private async Task RegistrarLogCorreoAsync(int entidadId, string numeroTramite, List<string> destinatarios, string asunto, string cuerpo, bool exitoso, string? error)
        {
            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string insertLogQuery = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM80_LOG_CORREOS]
                    (TM80_TM02_CODIGO, TM80_NUMERO_TRAMITE, TM80_DESTINATARIOS, TM80_ASUNTO, TM80_CUERPO, TM80_TIPO_CORREO, TM80_ESTADO_ENVIO, TM80_FECHA_ENVIO, TM80_USUARIO, TM80_ERROR_DETALLE)
                    VALUES (@entidadId, @numeroTramite, @destinatarios, @asunto, @cuerpo, 'APROBACION_DOCUMENTOS', @estado, GETDATE(), 'USUARIOWEB', @error)";

                using var command = new SqlCommand(insertLogQuery, connection);
                command.Parameters.AddWithValue("@entidadId", entidadId);
                command.Parameters.AddWithValue("@numeroTramite", numeroTramite);
                command.Parameters.AddWithValue("@destinatarios", string.Join(";", destinatarios));
                command.Parameters.AddWithValue("@asunto", asunto);
                command.Parameters.AddWithValue("@cuerpo", cuerpo.Length > 4000 ? cuerpo.Substring(0, 4000) : cuerpo);
                command.Parameters.AddWithValue("@estado", exitoso ? "ENVIADO" : "ERROR");
                command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar log de correo");
            }
        }
    }
}
