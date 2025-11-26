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

            // --- 2. Obtener datos de la base de datos ---
            var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            var dbService = new DatabaseService(connectionString);
            
            List<EntidadFiltro> entidadesFiltradas;
            try
            {
                entidadesFiltradas = await dbService.GetEntidadesFiltradasAsync(sectorId, estadoId, estadoIds);
            }
            catch (Exception ex)
            {
                log.LogError($"Error al consultar base de datos: {ex.Message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // --- 3. Retornar la lista de entidades usando HttpResponseData ---

            // ⭐️ Crear la respuesta
            var response = req.CreateResponse(HttpStatusCode.OK);

            // ⭐️ Serializar el objeto a JSON y escribirlo en el cuerpo de la respuesta
            try
            {
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(entidadesFiltradas));
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error al serializar la respuesta: {ex.Message}");
                // Si falla, retornamos un error 500
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            return response;
        }


    }
}