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
    public class SubirComprobantePago
    {
        private readonly ILogger _logger;

        public SubirComprobantePago(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubirComprobantePago>();
        }

        [Function("SubirComprobantePago")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubirComprobantePago")] HttpRequestData req)
        {
            _logger.LogInformation("Subiendo comprobante de pago");

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
                string? fileName = data?.nombreArchivo ?? "comprobante.pdf";
                decimal? valor = data?.valor;
                string? fechaPagoStr = data?.fechaPago;
                // Obtener usuario del body de la petición
                string usuario = data?.usuario ?? "Sistema";
                _logger.LogInformation($"Usuario recibido: {usuario}");
                
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

                string blobName = $"{nit}/{DateTime.UtcNow:yyyy-MM-dd}_{Guid.NewGuid()}_EXTRACTO_{fileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                using (var stream = new MemoryStream(fileData))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                // Guardar registro en TN07_Adjuntos y TN09_Extractos
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("INSERT INTO [SIIR-ProdV1].[dbo].[TN07_Adjuntos] (TN07_TM02_Codigo, TN07_Archivo, TN07_Fecha) OUTPUT INSERTED.TN07_Id VALUES (@entidadId, @archivo, @fecha)", conn);
                    cmd.Parameters.AddWithValue("@entidadId", entidadId);
                    cmd.Parameters.AddWithValue("@archivo", blobClient.Uri.ToString());
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    var tn07Id = await cmd.ExecuteScalarAsync();
                    
                    // Registrar en TN09_Extractos si hay valor y fecha
                    if (valor.HasValue && !string.IsNullOrEmpty(fechaPagoStr) && tn07Id != null)
                    {
                        DateTime fechaPago = DateTime.Parse(fechaPagoStr);
                        var cmdExtracto = new SqlCommand("INSERT INTO [SIIR-ProdV1].[dbo].[TN09_Extractos] (TN09_Fecha, TN09_Valor, TN09_TN07_Id) VALUES (@fecha, @valor, @tn07Id)", conn);
                        cmdExtracto.Parameters.AddWithValue("@fecha", fechaPago);
                        cmdExtracto.Parameters.Add("@valor", System.Data.SqlDbType.Decimal).Value = valor.Value;
                        cmdExtracto.Parameters.AddWithValue("@tn07Id", tn07Id);
                        await cmdExtracto.ExecuteNonQueryAsync();
                    }
                }
                
                // Registrar en historial
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    
                    // Obtener estado y tipo actual para el historial
                    var cmdEstado = new SqlCommand("SELECT TOP 1 TN05_TM01_EstadoActual, TN05_TM02_Tipo FROM [SIIR-ProdV1].[dbo].[TN05_Historico_Estado] WHERE TN05_TM02_Codigo = @entidadId ORDER BY TN05_Fecha DESC", conn);
                    cmdEstado.Parameters.AddWithValue("@entidadId", entidadId);
                    
                    int estadoActual = 13;
                    int tipoHistorial = 2;
                    
                    using (var reader = await cmdEstado.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            estadoActual = Convert.ToInt32(reader["TN05_TM01_EstadoActual"]);
                            tipoHistorial = Convert.ToInt32(reader["TN05_TM02_Tipo"]);
                        }
                    }
                    
                    // Registrar en historial
                    var cmdHistorial = new SqlCommand("INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado] (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones) VALUES (@tipo, @codigo, @estadoAnterior, @estadoActual, @fecha, @usuario, @observaciones)", conn);
                    cmdHistorial.Parameters.AddWithValue("@tipo", tipoHistorial);
                    cmdHistorial.Parameters.AddWithValue("@codigo", entidadId);
                    cmdHistorial.Parameters.AddWithValue("@estadoAnterior", estadoActual);
                    cmdHistorial.Parameters.AddWithValue("@estadoActual", estadoActual);
                    cmdHistorial.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmdHistorial.Parameters.AddWithValue("@usuario", usuario);
                    cmdHistorial.Parameters.AddWithValue("@observaciones", "Extracto de pago adjuntado");
                    await cmdHistorial.ExecuteNonQueryAsync();
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new { url = blobClient.Uri.ToString() }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir comprobante de pago");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }


    }
}