using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConfirmarPago
    {
        private readonly ILogger _logger;

        public ConfirmarPago(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConfirmarPago>();
        }

        [Function("ConfirmarPago")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ConfirmarPago")] HttpRequestData req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                using (JsonDocument doc = JsonDocument.Parse(requestBody))
                {
                    JsonElement root = doc.RootElement;
                    
                    int entidadId = root.TryGetProperty("EntidadId", out var entidadIdProp) ? entidadIdProp.GetInt32() : 0;
                    string numeroTramite = root.TryGetProperty("NumeroTramite", out var numeroTramiteProp) ? numeroTramiteProp.GetString() ?? "" : "";
                    // Obtener usuario del payload (acepta 'usuario' o 'Funcionario')
                    string funcionario = "Sistema";
                    if (root.TryGetProperty("usuario", out var usuarioProp))
                        funcionario = usuarioProp.GetString() ?? "Sistema";
                    else if (root.TryGetProperty("Funcionario", out var funcionarioProp))
                        funcionario = funcionarioProp.GetString() ?? "Sistema";
                    
                    _logger.LogInformation($"Usuario recibido: {funcionario}");

                    string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        // Obtener estado actual y nombre de la entidad
                        string getInfoQuery = "SELECT TM02_TM01_CODIGO, TM02_NOMBRE FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] WHERE TM02_CODIGO = @entidadId";
                        int estadoAnterior = 0;
                        string nombreEntidad = "";

                        using (var command = new SqlCommand(getInfoQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    estadoAnterior = reader["TM02_TM01_CODIGO"] != DBNull.Value ? Convert.ToInt32(reader["TM02_TM01_CODIGO"]) : 0;
                                    nombreEntidad = reader["TM02_NOMBRE"]?.ToString() ?? "";
                                }
                            }
                        }

                        // Obtener correos del SSD (59030) desde TM03_Usuario
                        List<string> correosSSD = new();
                        string correosSSDQuery = @"
                            SELECT DISTINCT TM03_Correo
                            FROM [SIIR-ProdV1].[dbo].[TM03_Usuario]
                            WHERE TM03_TM02_Codigo = '59030' 
                            AND TM03_Correo IS NOT NULL 
                            AND TM03_Correo != ''";

                        using (var command = new SqlCommand(correosSSDQuery, connection))
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
                                            correosSSD.Add(email);
                                        }
                                    }
                                }
                            }
                        }

                        if (!correosSSD.Contains("fogafin@fogafin.gov.co"))
                        {
                            correosSSD.Add("fogafin@fogafin.gov.co");
                        }

                        // Cambiar estado a 14
                        string updateQuery = @"
                            UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
                            SET TM02_TM01_CODIGO = 14
                            WHERE TM02_CODIGO = @entidadId AND TM02_TM01_CODIGO != 14";

                        // Usar el usuario recibido directamente
                        string usuarioFinal = string.IsNullOrEmpty(funcionario) ? "Sistema" : funcionario;

                        using (var command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Siempre registrar en histórico
                        string insertHistoricoQuery = @"
                            INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado]
                            (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                            VALUES (1, @entidadId, @estadoAnterior, 14, GETDATE(), @usuario, 'Confirmación de pago - Cambio a pendiente de aprobación final')";

                        using (var command2 = new SqlCommand(insertHistoricoQuery, connection))
                        {
                            command2.Parameters.AddWithValue("@entidadId", entidadId);
                            command2.Parameters.AddWithValue("@estadoAnterior", estadoAnterior);
                            command2.Parameters.AddWithValue("@usuario", usuarioFinal);
                            await command2.ExecuteNonQueryAsync();
                        }

                        // Log de notificación
                        string asunto = "Confirmación de Pago de Inscripción";
                        string cuerpo = $@"Estimados miembros del Departamento de Sistema de Seguro de Depósitos:

Nos permitimos informarles que el Departamento de DOT ha realizado la validación del pago de inscripción de la entidad {nombreEntidad}, le agradecemos realizar la gestión pertinente dentro de los diferentes aplicativos.

Cordial saludo,

Departamento de Sistema de Seguro de Depósitos
Fondo de Garantías de Instituciones Financieras – Fogafín
PBX: 601 4321370 extensiones 255 - 142";

                        _logger.LogInformation("=== NOTIFICACIÓN DE CONFIRMACIÓN DE PAGO ===");
                        _logger.LogInformation($"Asunto: {asunto}");
                        _logger.LogInformation($"Destinatarios: {string.Join(", ", correosSSD)}");
                        _logger.LogInformation($"Cuerpo:\n{cuerpo}");
                        _logger.LogInformation("==============================");


                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, message = "Pago confirmado correctamente" }));
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al confirmar pago");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}
