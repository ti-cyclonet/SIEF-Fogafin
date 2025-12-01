using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConsultarEstados
    {
        [Function("ConsultarEstados")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ConsultarEstados")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("ConsultarEstados");
            logger.LogInformation("Consultando estados");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string query = "SELECT TM01_Codigo as id, TM01_Nombre as nombre FROM TM01_Estado ORDER BY TM01_Nombre";

                using var command = new SqlCommand(query, connection);
                var estados = new List<object>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    estados.Add(new
                    {
                        id = reader.GetInt32(0),
                        nombre = reader.GetString(1)
                    });
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(estados));

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al consultar estados");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}