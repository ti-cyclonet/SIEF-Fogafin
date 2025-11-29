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
                bool esAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

                // Intentar obtener usuario desde cabeceras de autenticación integrada
                if (req.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var authHeader = authHeaders.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Negotiate"))
                    {
                        // Procesar token de autenticación integrada (simplificado)
                        _logger.LogInformation("Autenticación integrada detectada");
                    }
                }

                // Verificar cabecera personalizada del usuario
                if (req.Headers.TryGetValues("X-User-Identity", out var userHeaders))
                {
                    var userFromHeader = userHeaders.FirstOrDefault();
                    if (!string.IsNullOrEmpty(userFromHeader))
                    {
                        usuario = userFromHeader.Contains("\\") ? userFromHeader.Split('\\').Last() : userFromHeader;
                        _logger.LogInformation($"Usuario desde cabecera: {usuario}");
                    }
                }
                else if (esAzure)
                {
                    usuario = Environment.GetEnvironmentVariable("SIEF_USER") ?? "adminSief";
                    _logger.LogInformation($"Entorno Azure - Usuario configurado: {usuario}");
                }
                else
                {
                    try
                    {
                        var identity = WindowsIdentity.GetCurrent();
                        if (identity != null && !string.IsNullOrEmpty(identity.Name))
                        {
                            usuario = identity.Name.Contains("\\") ? identity.Name.Split('\\').Last() : identity.Name;
                            _logger.LogInformation($"Usuario local obtenido: {usuario}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error obteniendo usuario local: {ex.Message}");
                    }
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