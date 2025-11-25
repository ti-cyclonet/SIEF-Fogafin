using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Principal;

namespace InscripcionEntidades
{
    public class ObtenerUsuarioWindows
    {
        private readonly ILogger _logger;

        public ObtenerUsuarioWindows(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ObtenerUsuarioWindows>();
        }

        [Function("ObtenerUsuarioWindows")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "whoami")] HttpRequestData req)
        {
            _logger.LogInformation("Obteniendo usuario de Windows");

            try
            {
                string usuario = "adminSief";

                // Obtener usuario de Windows Identity (método que funciona)
                try
                {
                    var identity = WindowsIdentity.GetCurrent();
                    if (identity != null && !string.IsNullOrEmpty(identity.Name))
                    {
                        usuario = identity.Name;
                        if (usuario.Contains("\\"))
                        {
                            usuario = usuario.Split('\\').Last();
                        }
                        _logger.LogInformation($"Usuario obtenido: {usuario}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Usando usuario por defecto: {ex.Message}");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain");
                await response.WriteStringAsync(usuario);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario de Windows");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("adminSief");
                return errorResponse;
            }
        }
    }
}