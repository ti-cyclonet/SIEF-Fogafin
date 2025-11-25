using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConsultarEntidadesPorSector
    {
        private readonly ILogger _logger;

        public ConsultarEntidadesPorSector(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarEntidadesPorSector>();
        }

        [Function("ConsultarEntidadesPorSector")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "entidades-por-sector/{sectorId}")] HttpRequestData req,
            string sectorId)
        {
            _logger.LogInformation($"Consultando entidades para el sector: {sectorId}");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            e.TM02_Codigo as Id,
                            e.TM02_Nombre as Nombre,
                            e.TM02_TM01_CodigoSectorF as SectorId
                        FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] e
                        WHERE LTRIM(RTRIM(ISNULL(e.TM02_TM01_CodigoSectorF, ''))) = @sectorId
                        ORDER BY e.TM02_Nombre";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@sectorId", sectorId.Trim());

                        var entidades = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                entidades.Add(new
                                {
                                    id = reader["Id"].ToString(),
                                    nombre = reader["Nombre"].ToString(),
                                    sectorId = reader["SectorId"].ToString()
                                });
                            }
                        }

                        _logger.LogInformation($"Se encontraron {entidades.Count} entidades para el sector {sectorId}");

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(entidades));

                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar entidades por sector");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}