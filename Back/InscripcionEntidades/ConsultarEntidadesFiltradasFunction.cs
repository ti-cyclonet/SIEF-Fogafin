using InscripcionEntidades.DTOs;
using InscripcionEntidades.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace InscripcionEntidades.Functions
{
    public static class ConsultarEntidadesFiltradasFunction
    {
        // ⭐️ La firma del método ahora es asíncrona y usa HttpResponseData
        [Function("ConsultarEntidadesFiltradas")]
        public static async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "entidades-filtradas")] HttpRequestData req, FunctionContext executionContext)
        {
            var log = executionContext.GetLogger("ConsultarEntidadesFiltradas");
            log.LogInformation("C# HTTP trigger function 'ConsultarEntidadesFiltradas' processed a request.");

            // --- 1. Obtener parámetros de consulta ---
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string sectorIdStr = query["sectorId"];
            string estadoIdStr = query["estadoId"];
            string estadoIdsStr = query["estadoIds"];

            int? sectorId = null;
            if (int.TryParse(sectorIdStr, out int sId)) { sectorId = sId; }

            int? estadoId = null;
            if (int.TryParse(estadoIdStr, out int eId)) { estadoId = eId; }
            
            List<int> estadoIds = null;
            if (!string.IsNullOrEmpty(estadoIdsStr))
            {
                estadoIds = estadoIdsStr.Split(',').Where(s => int.TryParse(s.Trim(), out _)).Select(s => int.Parse(s.Trim())).ToList();
            }

            // --- 2. Crear respuesta inmediatamente ---
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            try
            {
                // --- 3. Obtener datos de la base de datos ---
                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                var dbService = new DatabaseService(connectionString);
                
                var entidadesFiltradas = await dbService.GetEntidadesFiltradasAsync(sectorId, estadoId, estadoIds);
                
                // --- 4. Serializar y escribir respuesta ---
                await response.WriteStringAsync(JsonSerializer.Serialize(entidadesFiltradas));
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("{\"error\":\"Error interno del servidor\"}");
                return errorResponse;
            }
        }


    }
}