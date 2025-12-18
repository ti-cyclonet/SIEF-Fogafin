using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ObtenerIdArchivo
    {
        private readonly ILogger _logger;

        public ObtenerIdArchivo(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ObtenerIdArchivo>();
        }

        [Function("ObtenerIdArchivo")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ObtenerIdArchivo")] HttpRequestData req)
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string entidadId = query["entidadId"];
                string url = query["url"];

                if (string.IsNullOrEmpty(entidadId) || string.IsNullOrEmpty(url))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Parámetros entidadId y url son requeridos");
                    return badRequest;
                }

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string queryArchivo = @"
                        SELECT TN07_Id 
                        FROM [SIIR-ProdV1].[dbo].[TN07_Adjuntos] 
                        WHERE TN07_TM02_Codigo = @entidadId AND TN07_Archivo = @url";

                    using (var command = new SqlCommand(queryArchivo, connection))
                    {
                        command.Parameters.AddWithValue("@entidadId", entidadId);
                        command.Parameters.AddWithValue("@url", url);

                        var result = await command.ExecuteScalarAsync();
                        
                        if (result != null)
                        {
                            var response = req.CreateResponse(HttpStatusCode.OK);
                            response.Headers.Add("Content-Type", "application/json");
                            await response.WriteStringAsync(JsonSerializer.Serialize(new { id = result.ToString() }));
                            return response;
                        }
                        else
                        {
                            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                            await notFound.WriteStringAsync("Archivo no encontrado");
                            return notFound;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ID del archivo");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}