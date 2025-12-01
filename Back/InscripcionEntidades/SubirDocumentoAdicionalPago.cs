using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Azure.Storage.Blobs;
using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;

namespace InscripcionEntidades
{
    public class SubirDocumentoAdicionalPago
    {
        private readonly ILogger _logger;

        public SubirDocumentoAdicionalPago(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubirDocumentoAdicionalPago>();
        }

        [Function("SubirDocumentoAdicionalPago")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubirDocumentoAdicionalPago")] HttpRequestData req)
        {
            _logger.LogInformation("Subiendo documento adicional de pago");

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var entidadId = query["entidadId"];

                if (string.IsNullOrEmpty(entidadId))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("EntidadId es requerido");
                    return badResponse;
                }

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic? data = JsonConvert.DeserializeObject(requestBody);
                
                string? base64File = data?.archivoBase64;
                string? fileName = data?.nombreArchivo ?? "documento.pdf";
                decimal? valor = data?.valor;
                string? fechaPagoStr = data?.fechaPago;
                
                if (string.IsNullOrWhiteSpace(base64File))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Archivo es requerido");
                    return badResponse;
                }

                byte[] fileData = Convert.FromBase64String(base64File);

                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                string nit = "";
                int tipoEntidad = 12;
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("SELECT TM02_NIT, TM02_TM01_CodigoSectorF FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] WHERE TM02_CODIGO = @entidadId", conn);
                    cmd.Parameters.AddWithValue("@entidadId", entidadId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            nit = reader["TM02_NIT"]?.ToString() ?? "";
                            if (int.TryParse(reader["TM02_TM01_CodigoSectorF"]?.ToString(), out int tipo))
                                tipoEntidad = tipo;
                        }
                    }
                }

                // Subir al blob en carpeta Adjuntos/{NIT}
                string storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
                var blobServiceClient = new BlobServiceClient(storageConnection);
                var containerClient = blobServiceClient.GetBlobContainerClient("adjuntos");
                await containerClient.CreateIfNotExistsAsync();

                string blobName = $"{nit}/{DateTime.UtcNow:yyyy-MM-dd}_{Guid.NewGuid()}_PAGO_ADICIONAL_{fileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                using (var stream = new MemoryStream(fileData))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }
                
                // Obtener datos adicionales del request
                DateTime? fechaPago = data?.fechaPago != null ? DateTime.Parse(data.fechaPago.ToString()) : null;
                
                // Guardar registro en TN07_Adjuntos
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("INSERT INTO [SIIR-ProdV1].[dbo].[TN07_Adjuntos] (TN07_TM02_Codigo, TN07_Archivo, TN07_Fecha) OUTPUT INSERTED.TN07_Id VALUES (@entidadId, @archivo, @fecha)", conn);
                    cmd.Parameters.AddWithValue("@entidadId", entidadId);
                    cmd.Parameters.AddWithValue("@archivo", blobClient.Uri.ToString());
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    var tn07Id = await cmd.ExecuteScalarAsync();
                    
                    if (valor.HasValue && fechaPago.HasValue && tn07Id != null)
                    {
                        var cmdPago = new SqlCommand("INSERT INTO [SIIR-ProdV1].[dbo].[TN06_Pagos] (TN06_TM02_Tipo, TN06_TM02_Codigo, TN06_Fecha, TN06_Valor, TN06_Comprobante) VALUES (@tipo, @codigo, @fecha, @valor, @comprobante)", conn);
                        cmdPago.Parameters.AddWithValue("@tipo", tipoEntidad);
                        cmdPago.Parameters.AddWithValue("@codigo", entidadId);
                        cmdPago.Parameters.AddWithValue("@fecha", fechaPago.Value);
                        cmdPago.Parameters.Add("@valor", System.Data.SqlDbType.Decimal).Value = valor.Value;
                        cmdPago.Parameters.AddWithValue("@comprobante", tn07Id.ToString());
                        await cmdPago.ExecuteNonQueryAsync();
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new { url = blobClient.Uri.ToString() }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir documento adicional de pago");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}