using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using InscripcionEntidades.DTOs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InscripcionEntidades
{
    public class RegistrarEntidad
    {
        private readonly ILogger _logger;

        public RegistrarEntidad(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<RegistrarEntidad>();
        }

        [Function("RegistrarEntidad")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Solicitud recibida para registrar una entidad.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TN04EntidadDto? data;

            try
            {
                data = JsonConvert.DeserializeObject<TN04EntidadDto>(requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al deserializar el JSON.");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("El formato del JSON no es válido.");
                return badResponse;
            }

            if (data == null || string.IsNullOrWhiteSpace(data.Nombre) || string.IsNullOrWhiteSpace(data.Nit))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Datos obligatorios faltantes o inválidos.");
                return badResponse;
            }

            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                var configError = req.CreateResponse(HttpStatusCode.InternalServerError);
                await configError.WriteStringAsync("No se encontró la cadena de conexión en Azure.");
                return configError;
            }

            try
            {
                int consecutivo;
                int currentYear = DateTime.Now.Year;
                string tipoCodigo = data.TipoEntidad.ToString();
                string nombreResponsableAsignado = string.Empty;
                string localPdfPath = string.Empty;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    if (await NitExisteAsync(data.Nit))
                    {
                        var existsResponse = req.CreateResponse(HttpStatusCode.Conflict);
                        await existsResponse.WriteStringAsync("Ya existe una entidad con el NIT suministrado.");
                        return existsResponse;
                    }

                    int tm02Codigo;
                    string getMaxCodeQuery = @"
                    SELECT ISNULL(MAX(TM02_CODIGO), 99899) + 1
                    FROM dbo.TM02_ENTIDADFINANCIERA
                    WHERE TM02_CODIGO >= 99900";
                    
                    using (SqlCommand cmdMaxCode = new SqlCommand(getMaxCodeQuery, conn))
                    {
                        object? result = await cmdMaxCode.ExecuteScalarAsync();
                        tm02Codigo = Convert.ToInt32(result);
                    }

                    string insertTM02 = @"
                    INSERT INTO dbo.TM02_ENTIDADFINANCIERA 
                    (TM02_CODIGO, TM02_TM01_CODIGO, TM02_NIT, TM02_NOMBRE, TM02_FECHAINSCRIPCION, TM02_ACTIVO, TM02_TIPOINFORME)
                    VALUES (@Codigo, 12, @Nit, @Nombre, @Fecha, 0, '0100');";

                    using (SqlCommand cmdTM02 = new SqlCommand(insertTM02, conn))
                    {
                        cmdTM02.Parameters.AddWithValue("@Codigo", tm02Codigo);
                        cmdTM02.Parameters.AddWithValue("@Nit", data.Nit);
                        cmdTM02.Parameters.AddWithValue("@Nombre", data.Nombre);
                        cmdTM02.Parameters.AddWithValue("@Fecha", data.FechaConstitucion);
                        await cmdTM02.ExecuteNonQueryAsync();
                    }

                    string insertTM08 = @"
                    INSERT INTO dbo.TM08_ConsecutivoEnt (TM08_TM01_Codigo, TM08_Ano)
                    OUTPUT INSERTED.TM08_Consecutivo
                    VALUES (@TipoEntidad, @Ano);";

                    using (SqlCommand cmdTM08 = new SqlCommand(insertTM08, conn))
                    {
                        cmdTM08.Parameters.AddWithValue("@TipoEntidad", data.TipoEntidad);
                        cmdTM08.Parameters.AddWithValue("@Ano", currentYear);
                        object? result = await cmdTM08.ExecuteScalarAsync();
                        consecutivo = Convert.ToInt32(result);
                    }

                    int numeroTramite = int.Parse($"{consecutivo}{tipoCodigo}{currentYear}");

                    var (pdfUrl, localPath) = await GenerarYSubirResumenPdf(data, numeroTramite);
                    localPdfPath = localPath;

                    string updateTM02 = @"
                    UPDATE dbo.TM02_ENTIDADFINANCIERA SET
                        TM02_TM08_Consecutivo = @Consecutivo,
                        TM02_TM01_CodigoSectorF = @TipoEntidad,
                        TM02_Correo_Noti = @CorreoNoti,
                        TM02_Telefono_Rep = @TelefonoRep,
                        TM02_FechaConstitucion = @FechaConstitucion,
                        TM02_CapitalSuscrito = @CapitalSuscrito,
                        TM02_ValorPagado = @ValorPagado,
                        TM02_FechaPago = @FechaPago,
                        TM02_TL15_Codigo = @TipoDoc,
                        TM02_Identificacion_Rep = @IdentificacionRep,
                        TM02_Nombre_Rep = @NombreRep,
                        TM02_Apellido_Rep = @ApellidoRep,
                        TM02_Cargo_Rep = @CargoRep,
                        TM02_Correo_Rep = @CorreoRep,
                        TM02_Fecha = @Fecha,
                        TM02_CartaSolicitud = @CartaSolicitud,
                        TM02_CertificadoSuper = @CertificadoSuper,
                        TM02_NombreResponsable = @NombreResponsable,
                        TM02_CorreoResponsable = @CorreoResponsable,
                        TM02_TelefonoResponsable = @TelefonoResponsable,
                        TM02_RutaComprobantePago = @RutaComprobantePago,
                        TM02_RutaLogoEntidad = @RutaLogoEntidad,
                        TM02_PaginaWeb = @PaginaWeb,
                        TM02_RutaResumenPdf = @RutaResumenPdf
                    WHERE TM02_CODIGO = @TM02Codigo;";

                    using (SqlCommand cmdUpdate = new SqlCommand(updateTM02, conn))
                    {
                        cmdUpdate.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                        cmdUpdate.Parameters.AddWithValue("@Consecutivo", consecutivo);
                        cmdUpdate.Parameters.AddWithValue("@TipoEntidad", data.TipoEntidad);
                        cmdUpdate.Parameters.AddWithValue("@CorreoNoti", (object?)data.CorreoNoti ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@FechaConstitucion", data.FechaConstitucion);
                        cmdUpdate.Parameters.AddWithValue("@CapitalSuscrito", data.CapitalSuscrito);
                        cmdUpdate.Parameters.AddWithValue("@ValorPagado", data.ValorPagado);
                        cmdUpdate.Parameters.AddWithValue("@FechaPago", (object?)data.FechaPago ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@TipoDoc", data.TipoDoc);
                        cmdUpdate.Parameters.AddWithValue("@IdentificacionRep", data.IdentificacionRep);
                        cmdUpdate.Parameters.AddWithValue("@NombreRep", data.NombreRep);
                        cmdUpdate.Parameters.AddWithValue("@ApellidoRep", data.ApellidoRep);
                        cmdUpdate.Parameters.AddWithValue("@CargoRep", data.CargoRep);
                        cmdUpdate.Parameters.AddWithValue("@CorreoRep", data.CorreoRep);
                        cmdUpdate.Parameters.AddWithValue("@Fecha", data.Fecha);
                        cmdUpdate.Parameters.AddWithValue("@TelefonoRep", (object?)data.TelefonoRep ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@NombreResponsable", data.NombreResponsable);
                        cmdUpdate.Parameters.AddWithValue("@CorreoResponsable", data.CorreoResponsable);
                        cmdUpdate.Parameters.AddWithValue("@TelefonoResponsable", data.TelefonoResponsable);
                        cmdUpdate.Parameters.AddWithValue("@CartaSolicitud", (object?)data.CartaSolicitud ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@CertificadoSuper", (object?)data.CertificadoSuper ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@RutaComprobantePago", (object?)data.RutaComprobantePago ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@PaginaWeb", (object?)data.PaginaWeb ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@RutaLogoEntidad", (object?)data.RutaLogoEntidad ?? DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@RutaResumenPdf", pdfUrl);
                        await cmdUpdate.ExecuteNonQueryAsync();
                    }

                    if (data.ArchivosAdjuntos != null && data.ArchivosAdjuntos.Count > 0)
                    {
                        string insertAdjunto = @"
                        INSERT INTO dbo.TN07_Adjuntos (TN07_TM02_Codigo, TN07_Archivo, TN07_Fecha)
                        VALUES (@TM02Codigo, @Archivo, @Fecha);";

                        foreach (string archivo in data.ArchivosAdjuntos)
                        {
                            if (!string.IsNullOrEmpty(archivo))
                            {
                                using (SqlCommand cmdAdjunto = new SqlCommand(insertAdjunto, conn))
                                {
                                    cmdAdjunto.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                                    cmdAdjunto.Parameters.AddWithValue("@Archivo", archivo);
                                    cmdAdjunto.Parameters.AddWithValue("@Fecha", DateTime.Now);
                                    await cmdAdjunto.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                    var responseObj = new
                    {
                        TM08_Consecutivo = consecutivo,
                        TM08_TM01_Codigo = tipoCodigo,
                        TM08_Ano = currentYear,
                        TM03_Nombre = nombreResponsableAsignado,
                        Departamento = "SSD"
                    };

                    var okResponse = req.CreateResponse(HttpStatusCode.OK);
                    okResponse.Headers.Add("Content-Type", "application/json");
                    await okResponse.WriteStringAsync(JsonConvert.SerializeObject(responseObj));

                    if (File.Exists(localPdfPath))
                    {
                        File.Delete(localPdfPath);
                    }

                    return okResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar la entidad.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    Mensaje = "Error interno del servidor.",
                    Detalle = ex.Message
                }));
                return errorResponse;
            }
        }

        private async Task<bool> NitExisteAsync(string nit)
        {
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("No se encontró la cadena de conexión.");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM dbo.TM02_ENTIDADFINANCIERA WHERE TM02_NIT = @Nit";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Nit", nit);
                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<(string pdfUrl, string localPath)> GenerarYSubirResumenPdf(TN04EntidadDto data, int numeroTramite)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            string? storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
            if (string.IsNullOrEmpty(storageConnection))
            {
                throw new InvalidOperationException("No se encontró la cadena de conexión del almacenamiento.");
            }

            var blobServiceClient = new BlobServiceClient(storageConnection);
            var containerClient = blobServiceClient.GetBlobContainerClient("resumenes");
            await containerClient.CreateIfNotExistsAsync();

            string fileName = $"{data.Nit}_{numeroTramite}_resumen.pdf";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                    page.Header()
                        .Padding(10)
                        .Background("#003366")
                        .AlignCenter()
                        .Text("Resumen de Inscripción de Entidad")
                        .FontSize(18)
                        .Bold()
                        .FontColor("#FFFFFF");

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Border(1).BorderColor("#CCCCCC").Padding(20).Column(inner =>
                        {
                            inner.Spacing(6);
                            inner.Item().Text($"Número de Trámite: {numeroTramite}").Bold().FontSize(13).FontColor("#003366");
                            inner.Item().Text($"Razón Social: {data.Nombre}");
                            inner.Item().Text($"NIT: {data.Nit}");
                            inner.Item().Text($"Tipo de Entidad: {data.TipoEntidad}");
                            inner.Item().Text($"Capital Suscrito: ${data.CapitalSuscrito:N0}");
                            inner.Item().Text($"Valor Pagado: ${data.ValorPagado:N0}");
                            inner.Item().Text($"Representante: {data.NombreRep} {data.ApellidoRep}");
                            inner.Item().Text($"Correo: {data.CorreoNoti}");
                            inner.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}");
                        });
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text("© Fogafín - SIEF")
                        .FontSize(9)
                        .FontColor("#999999");
                });
            }).GeneratePdf(tempPath);

            var blobClient = containerClient.GetBlobClient(fileName);
            using (FileStream fs = File.OpenRead(tempPath))
            {
                await blobClient.UploadAsync(fs, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
                });
            }

            return (blobClient.Uri.ToString(), tempPath);
        }

        private async Task<bool> EnviarCorreoAsync(object payload, string? pdfPath = null)
        {
            try
            {
                string baseUrl = "https://fn-email-corp-dev-eus2-h5cjfbeud6h7axab.eastus2-01.azurewebsites.net";
                string apiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY") ?? "dev-12345";

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var payloadObj = JObject.FromObject(payload);
                var correosArray = payloadObj["correosArea"]?.ToObject<List<string>>() ?? new List<string>();
                if (!payloadObj.ContainsKey("to"))
                    payloadObj["to"] = JArray.FromObject(correosArray);

                var attachments = new List<object>();

                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);
                    string base64Pdf = Convert.ToBase64String(pdfBytes);

                    attachments.Add(new
                    {
                        fileName = Path.GetFileName(pdfPath),
                        contentType = "application/pdf",
                        contentBase64 = base64Pdf
                    });
                }

                var toList = payloadObj["to"]?.ToObject<List<string>>() ?? correosArray;

                var emailBody = new
                {
                    to = toList,
                    subject = payloadObj["subject"]?.ToString() ?? "Registro de entidad",
                    htmlBody = payloadObj["htmlBody"]?.ToString() ?? "<p>Registro completado</p>",
                    attachments = attachments
                };

                string jsonBody = JsonConvert.SerializeObject(emailBody, Formatting.None);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("/api/send-email", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo.");
                return false;
            }
        }
    }

    public class TN04EntidadDto
    {
        public int TipoEntidad { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public string CorreoNoti { get; set; } = string.Empty;
        public long IdentificacionRep { get; set; }
        public string TipoDoc { get; set; } = string.Empty;
        public string NombreRep { get; set; } = string.Empty;
        public string ApellidoRep { get; set; } = string.Empty;
        public string CargoRep { get; set; } = string.Empty;
        public string CorreoRep { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string? TelefonoRep { get; set; }
        public string? CartaSolicitud { get; set; }
        public string? CertificadoSuper { get; set; }
        public string NombreResponsable { get; set; } = string.Empty;
        public string CorreoResponsable { get; set; } = string.Empty;
        public string TelefonoResponsable { get; set; } = string.Empty;
        public DateTime FechaConstitucion { get; set; }
        public decimal CapitalSuscrito { get; set; }
        public decimal ValorPagado { get; set; }
        public DateTime? FechaPago { get; set; }
        public string? RutaComprobantePago { get; set; }
        public string? PaginaWeb { get; set; }
        public string? RutaLogoEntidad { get; set; }
        public string? SiglasDepartamento { get; set; }
        public List<string> ArchivosAdjuntos { get; set; } = new List<string>();
    }

    public class SubirArchivo
    {
        private readonly ILogger _logger;

        public SubirArchivo(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubirArchivo>();
        }

        [Function("SubirArchivo")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("El cuerpo de la solicitud está vacío.");
                    return badReq;
                }

                dynamic? data = JsonConvert.DeserializeObject(requestBody);
                string? nit = data?.nit;

                if (string.IsNullOrWhiteSpace(nit))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Debe incluir el campo 'nit' en el cuerpo JSON.");
                    return badReq;
                }

                string? base64File = data?.archivoBase64;
                string? fileName = data?.nombreArchivo ?? $"{Guid.NewGuid()}.bin";
                string contentType = data?.contentType ?? "application/octet-stream";

                if (string.IsNullOrWhiteSpace(base64File))
                {
                    var badFile = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badFile.WriteStringAsync("Debe incluir el archivo codificado en base64.");
                    return badFile;
                }

                byte[] fileBytes = Convert.FromBase64String(base64File);

                string storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
                var blobServiceClient = new BlobServiceClient(storageConnection);
                var containerClient = blobServiceClient.GetBlobContainerClient("adjuntos");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                string cleanName = Path.GetFileName(fileName).Replace(" ", "_").Replace(":", "_").Replace("/", "_");
                string uniqueName = $"{nit}/{DateTime.UtcNow:yyyy-MM-dd}_{Guid.NewGuid()}_{cleanName}";

                var blobClient = containerClient.GetBlobClient(uniqueName);
                using (var stream = new MemoryStream(fileBytes))
                {
                    await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType });
                }

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteStringAsync(blobClient.Uri.ToString());
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error: {ex.Message}");
                return error;
            }
        }
    }

    public class ConsultarNIT
    {
        private readonly ILogger _logger;

        public ConsultarNIT(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarNIT>();
        }

        [Function("ConsultarNIT")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ConsultarNIT/{nit}")] HttpRequestData req,
            string nit)
        {
            try
            {
                bool existe = await NitExisteAsync(nit);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { nit, existe });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar NIT.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error al consultar el NIT.");
                return errorResponse;
            }
        }

        private async Task<bool> NitExisteAsync(string nit)
        {
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("No se encontró la cadena de conexión.");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM [TM02_ENTIDADFINANCIERA] WHERE [TM02_NIT] = @Nit";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Nit", nit);
                    int count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }
    }

    public class ConsultarSectores
    {
        private readonly ILogger _logger;

        public ConsultarSectores(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarSectores>();
        }

        [Function("ConsultarSectores")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ConsultarSectores/{codigos}")] HttpRequestData req,
            string codigos)
        {
            try
            {
                var listaCodigos = codigos.Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

                if (listaCodigos.Count == 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("No se encontraron códigos válidos.");
                    return badRequest;
                }

                var sectores = await ObtenerSectoresAsync(listaCodigos);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(sectores);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar sectores.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error al consultar los sectores.");
                return errorResponse;
            }
        }

        private async Task<List<SectorFinanciero>> ObtenerSectoresAsync(List<string> codigos)
        {
            var sectores = new List<SectorFinanciero>();
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("No se encontró la cadena de conexión.");

            var parameters = new List<SqlParameter>();
            var sqlInClause = new List<string>();

            for (int i = 0; i < codigos.Count; i++)
            {
                var paramName = $"@p{i}";
                sqlInClause.Add(paramName);
                parameters.Add(new SqlParameter(paramName, codigos[i]));
            }

            string query = $@"
            SELECT [TM01_CODIGO], [TM01_NOMBRE]
            FROM [SIIR-ProdV1].[dbo].[TM01_SectorFinanciero]
            WHERE [TM01_CODIGO] IN ({string.Join(",", sqlInClause)})";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sectores.Add(new SectorFinanciero
                            {
                                Codigo = reader["TM01_CODIGO"].ToString() ?? string.Empty,
                                Nombre = reader["TM01_NOMBRE"].ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            return sectores;
        }
    }

    public class SectorFinanciero
    {
        [JsonPropertyName("TM01_CODIGO")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("TM01_NOMBRE")]
        public string Nombre { get; set; } = string.Empty;
    }
}
