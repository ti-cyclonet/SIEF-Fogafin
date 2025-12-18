using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Azure.Storage.Blobs;

namespace InscripcionEntidades
{
    public class DescargarArchivo
    {
        private readonly ILogger _logger;

        public DescargarArchivo(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DescargarArchivo>();
        }

        [Function("DescargarArchivo")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "DescargarArchivo")] HttpRequestData req)
        {
            _logger.LogInformation("Descargando archivo");

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var url = query["url"];
                var inline = query["inline"];

                if (string.IsNullOrEmpty(url))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("URL del archivo requerida");
                    return badRequest;
                }

                string connectionString = Environment.GetEnvironmentVariable("StorageConnectionString");
                var blobServiceClient = new BlobServiceClient(connectionString);

                // Extraer container y blob name de la URL
                var uri = new Uri(url);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var containerName = segments[0];
                var blobName = Uri.UnescapeDataString(string.Join("/", segments.Skip(1)));
                


                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Archivo no encontrado");
                    return notFound;
                }

                var blobDownloadInfo = await blobClient.DownloadAsync();
                var contentType = blobDownloadInfo.Value.Details.ContentType ?? "application/octet-stream";
                
                // Forzar Content-Type correcto para PDFs
                if (blobName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "application/pdf";
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", contentType);
                
                var disposition = string.Equals(inline, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(inline, "1", StringComparison.OrdinalIgnoreCase)
                    ? "inline"
                    : "attachment";
                    
                response.Headers.Add("Content-Disposition", $"{disposition}; filename=\"{Path.GetFileName(blobName)}\"");

                await blobDownloadInfo.Value.Content.CopyToAsync(response.Body);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar archivo");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}