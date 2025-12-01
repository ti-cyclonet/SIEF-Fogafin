using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConsultarHistorialGestion
    {
        private readonly ILogger _logger;

        public ConsultarHistorialGestion(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarHistorialGestion>();
        }

        [Function("ConsultarHistorialGestion")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ConsultarHistorialGestion/{entidadId}")] HttpRequestData req,
            string entidadId)
        {
            _logger.LogInformation($"Consultando historial de gestión para entidad ID: {entidadId}");

            // Validar que entidadId no sea null, empty o "undefined"
            if (string.IsNullOrEmpty(entidadId) || entidadId == "undefined")
            {
                _logger.LogWarning("ID de entidad inválido o undefined");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("ID de entidad inválido");
                return badResponse;
            }

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            h.TN05_Fecha,
                            h.TN05_TN03_Usuario,
                            h.TN05_Observaciones,
                            ea.TM01_Nombre AS EstadoAnterior,
                            ec.TM01_Nombre AS EstadoActual
                        FROM [SIIR-ProdV1].[dbo].[TN05_Historico_Estado] h
                        LEFT JOIN [SIIR-ProdV1].[dbo].[TM01_Estado] ea ON h.TN05_TM01_EstadoAnterior = ea.TM01_Codigo
                        LEFT JOIN [SIIR-ProdV1].[dbo].[TM01_Estado] ec ON h.TN05_TM01_EstadoActual = ec.TM01_Codigo
                        WHERE h.TN05_TM02_Codigo = @entidadId
                        ORDER BY h.TN05_Fecha DESC";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@entidadId", entidadId);

                        var historial = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                historial.Add(new
                                {
                                    TN05_Fecha = reader["TN05_Fecha"],
                                    TN05_TN03_Usuario = reader["TN05_TN03_Usuario"]?.ToString(),
                                    TN05_Observaciones = reader["TN05_Observaciones"]?.ToString(),
                                    EstadoAnterior = reader["EstadoAnterior"]?.ToString(),
                                    EstadoActual = reader["EstadoActual"]?.ToString()
                                });
                            }
                        }

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(historial));

                        _logger.LogInformation($"Historial consultado exitosamente. {historial.Count} registros encontrados.");
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al consultar historial de gestión para entidad {entidadId}");

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}