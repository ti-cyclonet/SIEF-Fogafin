using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ActualizarCapitalSuscrito
    {
        private readonly ILogger _logger;

        public ActualizarCapitalSuscrito(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ActualizarCapitalSuscrito>();
        }

        [Function("ActualizarCapitalSuscrito")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ActualizarCapitalSuscrito")] HttpRequestData req)
        {
            _logger.LogInformation("Actualizando capital suscrito y valor pagado");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<ActualizarCapitalRequest>(requestBody);

                if (data == null || data.EntidadId <= 0 || data.CapitalSuscrito <= 0)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Datos inválidos");
                    return badResponse;
                }

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] 
                        SET TM02_CapitalSuscrito = @capitalSuscrito
                        WHERE TM02_CODIGO = @entidadId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@capitalSuscrito", data.CapitalSuscrito);
                        command.Parameters.AddWithValue("@entidadId", data.EntidadId);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            var response = req.CreateResponse(HttpStatusCode.OK);
                            response.Headers.Add("Content-Type", "application/json");
                            await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, message = "Capital actualizado correctamente" }));
                            return response;
                        }
                        else
                        {
                            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                            await notFoundResponse.WriteStringAsync("Entidad no encontrada");
                            return notFoundResponse;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar capital suscrito");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }

    public class ActualizarCapitalRequest
    {
        public int EntidadId { get; set; }
        public decimal CapitalSuscrito { get; set; }
    }
}