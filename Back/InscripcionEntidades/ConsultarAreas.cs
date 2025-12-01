using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConsultarAreas
    {
        private readonly ILogger _logger;

        public ConsultarAreas(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarAreas>();
        }

        [Function("ConsultarAreas")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "areas")] HttpRequestData req)
        {
            _logger.LogInformation("Consultando áreas disponibles");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT TM02_Codigo, TM02_Nombre FROM [SIIR-ProdV1].[dbo].[TM02_Area] ORDER BY TM02_Nombre";

                    using (var command = new SqlCommand(query, connection))
                    {
                        var areas = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                areas.Add(new
                                {
                                    TM02_Codigo = reader["TM02_Codigo"].ToString(),
                                    TM02_Nombre = reader["TM02_Nombre"].ToString()
                                });
                            }
                        }

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(areas));

                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar áreas");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}