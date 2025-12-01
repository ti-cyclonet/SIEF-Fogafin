using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;

namespace InscripcionEntidades
{
    public class ConsultarPagosEntidad
    {
        private readonly ILogger _logger;

        public ConsultarPagosEntidad(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarPagosEntidad>();
        }

        [Function("ConsultarPagosEntidad")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Consultando pagos de entidad");

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var entidadId = query["entidadId"];

                if (string.IsNullOrEmpty(entidadId))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("EntidadId es requerido");
                    return badResponse;
                }

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                var pagos = new List<object>();

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(@"
                        SELECT e.TN09_Fecha, e.TN09_Valor, a.TN07_Archivo
                        FROM [SIIR-ProdV1].[dbo].[TN09_Extractos] e
                        INNER JOIN [SIIR-ProdV1].[dbo].[TN07_Adjuntos] a ON e.TN09_TN07_Id = a.TN07_Id
                        WHERE a.TN07_TM02_Codigo = @entidadId
                        ORDER BY e.TN09_Fecha DESC", conn);
                    cmd.Parameters.AddWithValue("@entidadId", entidadId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            pagos.Add(new
                            {
                                fecha = reader["TN09_Fecha"],
                                valor = reader["TN09_Valor"],
                                archivo = reader["TN07_Archivo"]?.ToString()
                            });
                        }
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonConvert.SerializeObject(pagos));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar pagos de entidad");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}