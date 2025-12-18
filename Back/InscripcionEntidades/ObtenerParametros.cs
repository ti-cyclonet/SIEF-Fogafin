using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ObtenerParametros
    {
        private readonly ILogger _logger;

        public ObtenerParametros(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ObtenerParametros>();
        }

        [Function("ObtenerParametros")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parametros")] HttpRequestData req)
        {
            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                var parametros = new Dictionary<string, object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    string query = "SELECT TM00_Descripcion, TM00_Valor FROM [SIIR-ProdV1].[dbo].[TM00_ParametrosGenerales]";
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string descripcion = reader["TM00_Descripcion"]?.ToString() ?? "";
                                string valor = reader["TM00_Valor"]?.ToString() ?? "";
                                parametros[descripcion] = valor;
                            }
                        }
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(parametros));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener parámetros");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync("{\"error\":\"Error interno del servidor\"}");
                return errorResponse;
            }
        }
    }
}