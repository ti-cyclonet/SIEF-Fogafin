using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConsultarDestinatarios
    {
        private readonly ILogger _logger;

        public ConsultarDestinatarios(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarDestinatarios>();
        }

        [Function("ConsultarDestinatarios")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "destinatarios")] HttpRequestData req)
        {
            _logger.LogInformation("Consultando destinatarios por áreas específicas");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            r.TM04_Identificacion,
                            r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
                            r.TM04_EMail,
                            s.TM03_Nombre AS Area,
                            s.TM03_Codigo AS CodigoArea
                        FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                        INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                        INNER JOIN [SistemasComunes].[dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
                        WHERE c.TM15_TM12_TM01_Codigo = 17 
                        AND c.TM15_TM12_Ambiente = 'PROD'
                        AND r.TM04_Activo = 1
                        AND s.TM03_Codigo IN (52060, 52070, 59030)
                        ORDER BY s.TM03_Codigo, r.TM04_Nombre, r.TM04_Apellidos";

                    using (var command = new SqlCommand(query, connection))
                    {
                        var destinatarios = new List<object>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                destinatarios.Add(new
                                {
                                    TM04_Identificacion = reader["TM04_Identificacion"].ToString(),
                                    NombreCompleto = reader["NombreCompleto"].ToString(),
                                    TM04_EMail = reader["TM04_EMail"].ToString(),
                                    Area = reader["Area"].ToString(),
                                    CodigoArea = reader["CodigoArea"].ToString()
                                });
                            }
                        }

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(destinatarios));
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar destinatarios");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}