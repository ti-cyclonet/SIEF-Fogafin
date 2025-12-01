using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;

namespace InscripcionEntidades
{
    public class ObtenerArchivoDesdeId
    {
        private readonly ILogger _logger;

        public ObtenerArchivoDesdeId(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ObtenerArchivoDesdeId>();
        }

        [Function("ObtenerArchivoDesdeId")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ObtenerArchivoDesdeId/{tn07Id}")] HttpRequestData req,
            string tn07Id)
        {
            _logger.LogInformation($"Obteniendo archivo con TN07_Id: {tn07Id}");

            try
            {
                if (string.IsNullOrWhiteSpace(tn07Id))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("TN07_Id es requerido");
                    return badRequest;
                }

                string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    var configError = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await configError.WriteStringAsync("No se encontró la cadena de conexión");
                    return configError;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                    SELECT TN07_Archivo 
                    FROM dbo.TN07_Adjuntos 
                    WHERE TN07_Id = @TN07_Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TN07_Id", tn07Id);
                        object? result = await cmd.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                        {
                            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                            await notFound.WriteStringAsync("Archivo no encontrado");
                            return notFound;
                        }

                        string archivoUrl = result.ToString() ?? string.Empty;

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new { url = archivoUrl }));
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener archivo desde TN07_Id");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}
