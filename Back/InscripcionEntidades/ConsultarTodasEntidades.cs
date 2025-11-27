using InscripcionEntidades.DTOs;
using InscripcionEntidades.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace InscripcionEntidades.Functions
{
    public static class ConsultarTodasEntidades
    {
        [Function("ConsultarTodasEntidades")]
        public static async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "ConsultarTodasEntidades")] HttpRequestData req, FunctionContext executionContext)
        {
            var log = executionContext.GetLogger("ConsultarTodasEntidades");
            log.LogInformation("Consultando todas las entidades");

            var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            var dbService = new DatabaseService(connectionString);
            
            try
            {
                var entidades = await dbService.GetTodasLasEntidadesAsync();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(entidades));
                
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"Error al consultar entidades: {ex.Message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}