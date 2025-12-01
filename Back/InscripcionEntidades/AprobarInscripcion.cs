using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class AprobarInscripcion
    {
        private readonly ILogger _logger;

        public AprobarInscripcion(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AprobarInscripcion>();
        }

        [Function("AprobarInscripcion")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "AprobarInscripcion")] HttpRequestData req)
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

                        // Obtener estado actual
                        string getEstadoQuery = "SELECT TM02_TM01_CODIGO FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] WHERE TM02_CODIGO = @entidadId";
                        int estadoAnterior = 0;

                        using (var command = new SqlCommand(getEstadoQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    estadoAnterior = reader["TM02_TM01_CODIGO"] != DBNull.Value ? Convert.ToInt32(reader["TM02_TM01_CODIGO"]) : 0;
                                }
                            }
                        }

                        // Eliminar registros de notificación
                        string deleteNotificacionQuery = "DELETE FROM [SIIR-ProdV1].[dbo].[TM61_ENTIDADES_NOTIFICACION] WHERE TM61_TM02_CODIGO = @entidadId";
                        using (var command = new SqlCommand(deleteNotificacionQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Deshabilitar constraint y cambiar estado a 15
                        string disableConstraintQuery = "ALTER TABLE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] NOCHECK CONSTRAINT FK_TM02_ENTIDADFINANCIERA_TM01_SECTORFINANCIERO";
                        using (var command = new SqlCommand(disableConstraintQuery, connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }

                        string updateQuery = @"
                            UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
                            SET TM02_TM01_CODIGO = 15, TM02_FECHAINSCRIPCION = GETDATE()
                            WHERE TM02_CODIGO = @entidadId AND TM02_TM01_CODIGO != 15";

                        int rowsAffected = 0;
                        using (var command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@entidadId", entidadId);
                            rowsAffected = await command.ExecuteNonQueryAsync();
                        }

                        // Re-habilitar constraint
                        string enableConstraintQuery = "ALTER TABLE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] CHECK CONSTRAINT FK_TM02_ENTIDADFINANCIERA_TM01_SECTORFINANCIERO";
                        using (var command = new SqlCommand(enableConstraintQuery, connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                        
                        // Validar usuario
                        string usuarioFinal;
                        if (funcionario == "AdminSief")
                        {
                            usuarioFinal = "USUARIOWEB";
                        }
                        else
                        {
                            // Verificar si el usuario existe en TM03_Usuario
                            string checkUserQuery = "SELECT COUNT(*) FROM [SIIR-ProdV1].[dbo].[TM03_Usuario] WHERE TM03_Usuario = @usuario";
                            using (var checkCmd = new SqlCommand(checkUserQuery, connection))
                            {
                                checkCmd.Parameters.AddWithValue("@usuario", funcionario);
                                int userExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                                if (userExists == 0)
                                {
                                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                                    await errorResponse.WriteStringAsync($"El usuario '{funcionario}' no existe en el sistema");
                                    return errorResponse;
                                }
                            }
                            usuarioFinal = funcionario;
                        }

                        // Siempre registrar en histórico
                        string insertHistoricoQuery = @"
                            INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado]
                            (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                            VALUES (1, @entidadId, @estadoAnterior, 15, GETDATE(), @usuario, 'Aprobación de inscripción - Entidad inscrita')";

                        using (var command2 = new SqlCommand(insertHistoricoQuery, connection))
                        {
                            command2.Parameters.AddWithValue("@entidadId", entidadId);
                            command2.Parameters.AddWithValue("@estadoAnterior", estadoAnterior);
                            command2.Parameters.AddWithValue("@usuario", usuarioFinal);
                            await command2.ExecuteNonQueryAsync();
                        }

                        _logger.LogInformation($"Inscripción aprobada para entidad {entidadId}. Estado cambió de {estadoAnterior} a 15");
                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, message = "Inscripción aprobada correctamente" }));
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aprobar inscripción");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}
