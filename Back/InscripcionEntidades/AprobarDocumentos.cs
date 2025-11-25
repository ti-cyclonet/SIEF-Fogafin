using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

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
                    
                    int entidadId = root.TryGetProperty("EntidadId", out var entidadIdProp) ? entidadIdProp.GetInt32() : 0;
                    string numeroTramite = root.TryGetProperty("NumeroTramite", out var numeroTramiteProp) ? numeroTramiteProp.GetString() ?? "" : "";
                    string funcionario = root.TryGetProperty("Funcionario", out var funcionarioProp) ? funcionarioProp.GetString() ?? "" : "";

                    string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        // Obtener RazonSocial y estado anterior
                        string getInfoQuery = "SELECT TM02_NOMBRE, TM02_TM01_CODIGO FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] WHERE TM02_CODIGO = @entidadId";
                        string razonSocial = "";
                        int estadoAnterior = 0;

                        using (var command = new SqlCommand(getInfoQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    razonSocial = reader["TM02_NOMBRE"]?.ToString() ?? "";
                                    estadoAnterior = reader["TM02_TM01_CODIGO"] != DBNull.Value ? Convert.ToInt32(reader["TM02_TM01_CODIGO"]) : 0;
                                }
                            }
                        }

                        List<string> destinatarios = new();
                        string correosDOTQuery = @"
                            SELECT DISTINCT r.TM04_EMail
                            FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                            INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                            WHERE c.TM15_TM12_TM01_Codigo = 17 AND c.TM15_TM12_Ambiente = 'PROD' 
                            AND r.TM04_TM03_Codigo = 52050 
                            AND r.TM04_Activo = 1 
                            AND r.TM04_EMail IS NOT NULL 
                            AND r.TM04_EMail != ''";

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

                        _logger.LogInformation("=== NOTIFICACIÓN DE CORREO ===");
                        _logger.LogInformation($"Destinatarios: {string.Join(", ", destinatarios)}");
                        _logger.LogInformation($"Asunto: {asunto}");
                        _logger.LogInformation($"Cuerpo del mensaje:\n{cuerpo}");
                        _logger.LogInformation("==============================");

                        // Eliminar registros de TM61_ENTIDADES_NOTIFICACION para evitar conflictos FK
                        string deleteNotificacionesQuery = @"
                            DELETE FROM [SIIR-ProdV1].[dbo].[TM61_ENTIDADES_NOTIFICACION] 
                            WHERE TM61_TM02_Codigo = @entidadId";

                        using (var command = new SqlCommand(deleteNotificacionesQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Cambiar estado a "En validación del pago" (código 13)
                        string updateQuery = @"
                            UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
                            SET TM02_TM01_CODIGO = 13
                            WHERE TM02_CODIGO = @entidadId AND TM02_TM01_CODIGO != 13";

                        using (var command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            int rowsAffected = await command.ExecuteNonQueryAsync();
                            
                            if (rowsAffected > 0)
                            {
                                // Registrar en histórico de estados solo si se actualizó
                                string insertHistoricoQuery = @"
                                    INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado]
                                    (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                                    VALUES (1, @entidadId, @estadoAnterior, 13, GETDATE(), @funcionario, 'Aprobación de documentos - Cambio a validación de pago')";

                                using (var command2 = new SqlCommand(insertHistoricoQuery, connection))
                                {
                                    command2.Parameters.AddWithValue("@entidadId", entidadId);
                                    command2.Parameters.AddWithValue("@estadoAnterior", estadoAnterior);
                                    command2.Parameters.AddWithValue("@funcionario", funcionario);
                                    await command2.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // Registrar en log de correos
                        string insertLogQuery = @"
                            INSERT INTO [SIIR-ProdV1].[dbo].[TM80_LOG_CORREOS]
                            (TM80_TM02_CODIGO, TM80_NUMERO_TRAMITE, TM80_DESTINATARIOS, TM80_ASUNTO, TM80_CUERPO, TM80_TIPO_CORREO, TM80_ESTADO_ENVIO, TM80_FECHA_ENVIO)
                            VALUES (@entidadId, @numeroTramite, @destinatarios, @asunto, @cuerpo, 'APROBACION_DOCUMENTOS', 'SIMULADO', GETDATE())";

                        using (var command = new SqlCommand(insertLogQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            command.Parameters.AddWithValue("@numeroTramite", numeroTramite);
                            command.Parameters.AddWithValue("@destinatarios", string.Join(";", destinatarios));
                            command.Parameters.AddWithValue("@asunto", asunto);
                            command.Parameters.AddWithValue("@cuerpo", cuerpo);
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, message = "Documentos aprobados correctamente" }));
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
    }
}
