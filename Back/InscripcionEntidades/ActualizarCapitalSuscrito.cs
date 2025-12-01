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
                var data = JsonSerializer.Deserialize<ActualizarCapitalRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || data.EntidadId <= 0 || data.CapitalSuscrito <= 0 || string.IsNullOrWhiteSpace(data.Observaciones))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Datos inválidos o observaciones requeridas");
                    return badResponse;
                }

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Obtener el último estado de la entidad
                            string getLastStateQuery = @"
                                SELECT TOP 1 TN05_TM01_EstadoActual 
                                FROM [SIIR-ProdV1].[dbo].[TN05_Historico_Estado] 
                                WHERE TN05_TM02_Codigo = @entidadId 
                                ORDER BY TN05_Fecha DESC";
                            
                            int estadoActual = 0;
                            using (var stateCommand = new SqlCommand(getLastStateQuery, connection, transaction))
                            {
                                stateCommand.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                var result = await stateCommand.ExecuteScalarAsync();
                                if (result != null) estadoActual = Convert.ToInt32(result);
                            }

                            // Actualizar capital suscrito
                            string updateQuery = @"
                                UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] 
                                SET TM02_CapitalSuscrito = @capitalSuscrito
                                WHERE TM02_CODIGO = @entidadId";

                            using (var updateCommand = new SqlCommand(updateQuery, connection, transaction))
                            {
                                updateCommand.Parameters.AddWithValue("@capitalSuscrito", data.CapitalSuscrito);
                                updateCommand.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                                
                                if (rowsAffected == 0)
                                {
                                    transaction.Rollback();
                                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                                    await notFoundResponse.WriteStringAsync("Entidad no encontrada");
                                    return notFoundResponse;
                                }
                            }

                            // Insertar registro en historial
                            if (estadoActual > 0)
                            {
                                string insertHistoryQuery = @"
                                    INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado] 
                                    (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                                    VALUES (1, @entidadId, @estadoAnterior, @estadoActual, @fecha, @usuario, @observaciones)";

                                using (var historyCommand = new SqlCommand(insertHistoryQuery, connection, transaction))
                                {
                                    historyCommand.Parameters.AddWithValue("@entidadId", data.EntidadId);
                                    historyCommand.Parameters.AddWithValue("@estadoAnterior", estadoActual);
                                    historyCommand.Parameters.AddWithValue("@estadoActual", estadoActual);
                                    historyCommand.Parameters.AddWithValue("@fecha", DateTime.Now);
                                    historyCommand.Parameters.AddWithValue("@usuario", data.Usuario);
                                    historyCommand.Parameters.AddWithValue("@observaciones", data.Observaciones);
                                    await historyCommand.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                            var response = req.CreateResponse(HttpStatusCode.OK);
                            response.Headers.Add("Content-Type", "application/json");
                            await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, message = "Capital actualizado correctamente" }));
                            return response;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
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
        public string Observaciones { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
    }
}