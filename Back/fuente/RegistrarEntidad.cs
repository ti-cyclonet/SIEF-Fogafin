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

            // ## Validación de Entrada y Deserialización
            // -----------------------------------------------------------------------------
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

            // ## Lógica Principal de Inserción y Procesamiento
            // -----------------------------------------------------------------------------
            try
            {
                int consecutivo;
                int currentYear = DateTime.Now.Year;
                // Se asume que data.TipoEntidad es un int y el código necesita un string para el trámite
                string tipoCodigo = data.TipoEntidad.ToString();
                string nombreResponsableAsignado = string.Empty;
                string localPdfPath = string.Empty; // Inicializar para que esté disponible en el try/catch

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 🔹 Validar NIT duplicado
                    if (await NitExisteAsync(data.Nit))
                    {
                        var existsResponse = req.CreateResponse(HttpStatusCode.Conflict);
                        await existsResponse.WriteStringAsync("Ya existe una entidad con el NIT suministrado.");
                        return existsResponse;
                    }

                    // 🔹 Generar próximo código TM02 con prefijo 999
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

                    // 🔹 Insertar TM02_ENTIDADFINANCIERA con código generado
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

                    // 🔹 Insertar consecutivo TM08
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

                    // 🔹 Crear número de trámite
                    int numeroTramite = int.Parse($"{consecutivo}{tipoCodigo}{currentYear}");

                    // 🔹 Generar y subir el resumen PDF
                    var (pdfUrl, localPath) = await GenerarYSubirResumenPdf(data, numeroTramite);
                    localPdfPath = localPath; // Asignar a la variable de ámbito superior

                    // 🔹 Actualizar TM02_ENTIDADFINANCIERA con datos adicionales
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

                    // 🔹 Insertar adjuntos en TN07_Adjuntos
                    _logger.LogInformation($"Verificando adjuntos: {data.ArchivosAdjuntos?.Count ?? 0} archivos");
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
                                    _logger.LogInformation($"Adjunto insertado: {archivo}");
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No hay archivos adjuntos para insertar");
                    }

                    // -----------------------------------------------------------------------------
                    // 🔹 1. Obtención de datos de responsable y departamento asignado
                    // -----------------------------------------------------------------------------
                    // 🔹 Obtener nombre del responsable asignado
                    string responsableQuery = @"
                    SELECT TOP 1 TM04_Nombre + ' ' + TM04_Apellidos
                    FROM [SistemasComunes].[dbo].[TM04_Responsables]
                    WHERE TM04_TM03_Codigo IN (59030, 52060) AND TM04_Activo = 1
                    ORDER BY TM04_Nombre";

                    using (SqlCommand cmdResp = new SqlCommand(responsableQuery, conn))
                    {
                        object? respResult = await cmdResp.ExecuteScalarAsync();
                        if (respResult != null && respResult != DBNull.Value)
                        {
                            nombreResponsableAsignado = respResult.ToString()!;
                        }
                    }

                    string siglasDepartamento = string.Empty;
                    // 🔹 Obtener las siglas del departamento (TM02_Nombre) para el estado 12
                    string deptoQuery = @"
                    SELECT TOP 1 a.TM02_Nombre
                    FROM dbo.TM02_Area a
                    INNER JOIN dbo.TM01_Estado e ON a.TM02_Codigo = e.TM01_TM02_Codigo
                    WHERE e.TM01_Codigo = 12";

                    using (SqlCommand cmdDepto = new SqlCommand(deptoQuery, conn))
                    {
                        object? deptoResult = await cmdDepto.ExecuteScalarAsync();
                        if (deptoResult != null && deptoResult != DBNull.Value)
                        {
                            siglasDepartamento = deptoResult.ToString()!;
                        }
                    }

                    // -----------------------------------------------------------------------------
                    // 🔹 2. Preparación y Envío de Respuesta JSON
                    // -----------------------------------------------------------------------------

                    // 🔹 Preparar respuesta JSON
                    var responseObj = new
                    {
                        TM08_Consecutivo = consecutivo,
                        TM08_TM01_Codigo = tipoCodigo,
                        TM08_Ano = currentYear,
                        TM03_Nombre = nombreResponsableAsignado,
                        Departamento = siglasDepartamento
                    };

                    var okResponse = req.CreateResponse(HttpStatusCode.OK);
                    okResponse.Headers.Add("Content-Type", "application/json");
                    await okResponse.WriteStringAsync(JsonConvert.SerializeObject(responseObj));

                    // -----------------------------------------------------------------------------
                    // 🔹 3. Preparación de Variables para Correo Electrónico
                    // -----------------------------------------------------------------------------

                    // 🔹 Preparar datos para envío de correo
                    string representanteLegal = $"{data.NombreRep} {data.ApellidoRep}";
                    string entidadNombre = data.Nombre;
                    string numeroTramiteStr = $"{consecutivo}{tipoCodigo}{currentYear}";
                    string linkConsulta = "https://sadevsiefexterno.z20.web.core.windows.net/pages/consulta.html";

                    // -----------------------------------------------------------------------------
                    // 🔹 4. Consulta de Correos del Área Responsable
                    // -----------------------------------------------------------------------------

                    // 🔹 Consultar destinatarios por áreas específicas
                    List<string> correosArea = new();
                    var destinatariosPorArea = new Dictionary<string, List<(string nombre, string email)>>();
                    
                    string destinatariosQuery = @"
                    SELECT 
                        r.TM04_Nombre + ' ' + r.TM04_Apellidos AS NombreCompleto,
                        r.TM04_EMail,
                        s.TM03_Nombre AS Area,
                        s.TM03_Codigo AS CodigoArea
                    FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                    INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                    INNER JOIN [SistemasComunes].[dbo].[TM03_Subdirecciones] s ON r.TM04_TM03_Codigo = s.TM03_Codigo
                    WHERE c.TM15_TM12_TM01_Codigo = 17 
                    AND c.TM15_TM12_Ambiente = 'PROD'
                    AND r.TM04_Activo = 1
                    AND s.TM03_Codigo IN (52060, 52070, 59030)
                    ORDER BY s.TM03_Codigo, r.TM04_Nombre";

                    using (SqlCommand cmdDestinatarios = new SqlCommand(destinatariosQuery, conn))
                    {
                        using (SqlDataReader reader = await cmdDestinatarios.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string nombre = reader["NombreCompleto"].ToString() ?? "";
                                string email = reader["TM04_EMail"].ToString() ?? "";
                                string area = reader["Area"].ToString() ?? "";
                                string codigoArea = reader["CodigoArea"].ToString() ?? "";
                                
                                if (!string.IsNullOrEmpty(email))
                                {
                                    correosArea.Add(email);
                                    
                                    if (!destinatariosPorArea.ContainsKey(codigoArea))
                                        destinatariosPorArea[codigoArea] = new List<(string, string)>();
                                    
                                    destinatariosPorArea[codigoArea].Add((nombre, email));
                                }
                            }
                        }
                    }
                    
                    // Mostrar destinatarios por consola
                    _logger.LogInformation("📋 DESTINATARIOS AGRUPADOS POR ÁREA:");
                    foreach (var area in destinatariosPorArea.OrderBy(x => x.Key))
                    {
                        _logger.LogInformation($"\n🏢 ÁREA {area.Key}:");
                        foreach (var (nombre, email) in area.Value)
                        {
                            _logger.LogInformation($"  📧 {nombre} - {email}");
                        }
                    }
                    
                    // Mostrar plantillas de correo
                    string representanteLegal = $"{data.NombreRep} {data.ApellidoRep}";
                    string entidadNombre = data.Nombre;
                    string numeroTramiteStr = $"{consecutivo}{tipoCodigo}{currentYear}";
                    string linkConsulta = "https://sadevsiefexterno.z20.web.core.windows.net/pages/consulta.html";

                    _logger.LogInformation("\n📧 PLANTILLAS DE CORREO:");
                    
                    var plantillaArea = $@"
                    <p>Doctor(a) {representanteLegal},</p>
                    <p>La entidad <strong>{entidadNombre}</strong> ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín, con el número del trámite <strong>{numeroTramiteStr}</strong>.</p>
                    <p>Puede consultar el estado del trámite en el siguiente link: 
                       <a href='{linkConsulta}'>{linkConsulta}</a></p>
                    <p>Cordial saludo,<br/><br/>
 
                    Departamento de Sistema de Seguro de Depósitos<br/>
                    Fondo de Garantías de Instituciones Financieras – Fogafín<br/>
                    PBX: 601 4321370 extensiones 255 - 142</p>";

                    _logger.LogInformation($"\n🏢 PLANTILLA ÁREA RESPONSABLE:\n{plantillaArea}");

                    var plantillaUsuario = $@"
                    <p>Estimado(a) {representanteLegal},</p>
                    <p>Gracias por registrar la entidad <strong>{entidadNombre}</strong> en el Sistema de Inscripción de Entidades Financieras (SIEF).</p>
                    <p>El trámite se ha registrado exitosamente con el número <strong>{numeroTramiteStr}</strong>.</p>
                    <p>Puede consultar su estado en el siguiente enlace:</p>
                    <p><a href='{linkConsulta}'>{linkConsulta}</a></p>
                    <p>Atentamente,<br/><strong>Equipo Fogafín</strong></p>";

                    _logger.LogInformation($"\n👤 PLANTILLA USUARIO:\n{plantillaUsuario}");
                    //Se agrega el correo fogafin@fogafin.gov.co a la lista obtenida
                    //correosArea.Add("fogafin@fogafin.gov.co");

                    // -----------------------------------------------------------------------------
                    // 🔹 5. Envío de Correo al Área Responsable (Notificación Interna)
                    // -----------------------------------------------------------------------------

                    // 🔹 Armar JSON del correo a enviar
                    var emailPayload = new
                    {
                        representanteLegal = representanteLegal,
                        entidad = entidadNombre,
                        numeroTramite = numeroTramiteStr,
                        correosArea = correosArea,
                        linkConsulta = linkConsulta
                    };

                    // Mostrar el JSON en consola para pruebas locales
                    string emailPayloadJson = JsonConvert.SerializeObject(emailPayload, Formatting.Indented);
                    _logger.LogInformation("📧 JSON del correo a enviar:\n" + emailPayloadJson);

                    // Enviar correo al área responsable (sin adjunto)
                    bool correoAreaEnviado = await EnviarCorreoAsync(emailPayload);
                    _logger.LogInformation($"📨 Correo al área responsable enviado: {correoAreaEnviado}");

                    // -----------------------------------------------------------------------------
                    // 🔹 6. Envío de Correo al Usuario (Confirmación con PDF)
                    // -----------------------------------------------------------------------------

                    // Enviar correo al usuario (con el PDF adjunto)
                    var emailUsuarioPayload = new
                    {
                        representanteLegal = representanteLegal,
                        entidad = entidadNombre,
                        numeroTramite = numeroTramiteStr,
                        correosArea = new List<string> { data.CorreoNoti },
                        linkConsulta = linkConsulta
                    };

                    bool correoUsuarioEnviado = await EnviarCorreoAsync(emailUsuarioPayload, localPdfPath);
                    _logger.LogInformation($"📧 Correo de confirmación enviado al usuario {data.CorreoNoti}: {correoUsuarioEnviado}");

                    // -----------------------------------------------------------------------------
                    // 🔹 7. Envío de Correo al Usuario (Notificación Personalizada - Bloque Adicional)
                    // -----------------------------------------------------------------------------

                    // 🔹 Enviar correo al usuario (notificación personal)
                    if (!string.IsNullOrEmpty(data.CorreoNoti))
                    {
                        var emailUsuario = new
                        {
                            to = new List<string> { data.CorreoNoti },
                            subject = $"Confirmación de registro de la entidad {data.Nombre}",
                            htmlBody = $@"
                            <p>Estimado(a) {data.NombreRep} {data.ApellidoRep},</p>
                            <p>Gracias por registrar la entidad <strong>{data.Nombre}</strong> en el Sistema de Inscripción de Entidades Financieras (SIEF).</p>
                            <p>El trámite se ha registrado exitosamente con el número <strong>{numeroTramiteStr}</strong>.</p>
                            <p>Puede consultar su estado en el siguiente enlace:</p>
                            <p><a href='{linkConsulta}'>{linkConsulta}</a></p>
                            <p>Atentamente,<br/><strong>Equipo Fogafín</strong></p>",
                            correosArea = new List<string>()
                        };

                        bool correoUsuario = await EnviarCorreoAsync(emailUsuario, localPdfPath);
                        _logger.LogInformation($"Correo de confirmación enviado al usuario {data.CorreoNoti}: {correoUsuario}");
                    }
                    else
                    {
                        _logger.LogWarning("No se envió correo al usuario porque el campo CorreoNoti está vacío.");
                    }

                    // -----------------------------------------------------------------------------
                    // 🔹 8. Retorno de la Respuesta
                    // -----------------------------------------------------------------------------

                    // Limpiar el archivo PDF temporal después de usarlo.
                    if (File.Exists(localPdfPath))
                    {
                        File.Delete(localPdfPath);
                        _logger.LogInformation($"🗑️ Archivo PDF temporal eliminado: {localPdfPath}");
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


        // ## Funciones Auxiliares
        // -----------------------------------------------------------------------------

        // 🔍 Función auxiliar para consultar si un NIT existe en la tabla TM02_ENTIDADFINANCIERA
        private async Task<bool> NitExisteAsync(string nit)
        {
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("❌ No se encontró la cadena de conexión SqlConnectionString.");
                throw new InvalidOperationException("No se encontró la cadena de conexión.");
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                    SELECT COUNT(*) 
                    FROM dbo.TM02_ENTIDADFINANCIERA
                    WHERE TM02_NIT = @Nit";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Nit", nit);
                        // ExecuteScalarAsync devuelve object, se castea a int
                        int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar existencia del NIT en la base de datos.");
                throw;
            }
        }

        // 🧾 Método auxiliar para generar y subir el PDF
        private async Task<(string pdfUrl, string localPath)> GenerarYSubirResumenPdf(TN04EntidadDto data, int numeroTramite)
        {
            // Asegúrate de que QuestPDF esté configurado antes de usarlo
            QuestPDF.Settings.License = LicenseType.Community;

            string? storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
            if (string.IsNullOrEmpty(storageConnection))
            {
                _logger.LogError("❌ No se encontró la variable StorageConnectionString en las configuraciones.");
                throw new InvalidOperationException("No se encontró la cadena de conexión del almacenamiento.");
            }

            // Crear cliente de Blob Storage
            var blobServiceClient = new BlobServiceClient(storageConnection);
            // Contenedor para los resúmenes (asumiendo que existe o se puede crear)
            var containerClient = blobServiceClient.GetBlobContainerClient("resumenes");
            await containerClient.CreateIfNotExistsAsync();

            string fileName = $"{data.Nit}_{numeroTramite}_resumen.pdf";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            _logger.LogInformation($"Iniciando generación del PDF en ruta temporal: {tempPath}");

            try
            {
                // 📄 Generar PDF con QuestPDF
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
                                inner.Item().Text($"📄 Número de Trámite: {numeroTramite}").Bold().FontSize(13).FontColor("#003366");
                                inner.Item().Text($"🏢 Razón Social: {data.Nombre}");
                                inner.Item().Text($"🔢 NIT: {data.Nit}");
                                inner.Item().Text($"🏛️ Tipo de Entidad: {data.TipoEntidad}");
                                // Asegúrate de que las propiedades de moneda y fecha son manejadas correctamente en tu DTO
                                inner.Item().Text($"💰 Capital Suscrito: {data.CapitalSuscrito:C}");
                                inner.Item().Text($"💸 Valor Pagado: {data.ValorPagado:C}");
                                inner.Item().Text($"👤 Representante: {data.NombreRep} {data.ApellidoRep}");
                                inner.Item().Text($"📧 Correo de Notificación: {data.CorreoNoti}");
                                inner.Item().Text($"📅 Fecha de Registro: {DateTime.Now:dd/MM/yyyy}");
                            });

                            col.Item().PaddingTop(15).AlignCenter()
                                .Text("Este documento fue generado automáticamente por el Sistema de Inscripción de Entidades Financieras (SIEF).")
                                .FontSize(9)
                                .Italic()
                                .FontColor("#666666");
                        });

                        page.Footer()
                            .AlignCenter()
                            .Text("© Fogafín - Sistema de Inscripción de Entidades Financieras")
                            .FontSize(9)
                            .FontColor("#999999");
                    });
                }).GeneratePdf(tempPath);

                _logger.LogInformation("PDF generado correctamente en el almacenamiento temporal.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error durante la generación del PDF.");
                throw;
            }

            try
            {
                // ☁️ Subir PDF a Azure Blob Storage
                var blobClient = containerClient.GetBlobClient(fileName);

                using (FileStream fs = File.OpenRead(tempPath))
                {
                    await blobClient.UploadAsync(
                        fs,
                        new BlobUploadOptions
                        {
                            HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
                        }
                    );
                }

                _logger.LogInformation($"Archivo PDF subido correctamente a Blob Storage: {blobClient.Uri}");

                return (blobClient.Uri.ToString(), tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al subir el PDF a Blob Storage.");
                throw;
            }
        }

        // 📧 Método auxiliar para enviar correo a través de un servicio externo (otra Azure Function)
        private async Task<bool> EnviarCorreoAsync(object payload, string? pdfPath = null)
        {
            try
            {
                string baseUrl = "https://fn-email-corp-dev-eus2-h5cjfbeud6h7axab.eastus2-01.azurewebsites.net";
                string apiKey = Environment.GetEnvironmentVariable("EMAIL_API_KEY") ?? "dev-12345";
                _logger.LogInformation("Iniciando proceso de envío de correo mediante Azure Function Email Service.");

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                // Usa JObject para manipular el payload de forma dinámica
                var payloadObj = JObject.FromObject(payload);
                var correosArray = payloadObj["correosArea"]?.ToObject<List<string>>() ?? new List<string>();
                // Si 'to' no está en el payload (se usa en el bloque 7), se rellena con correosArea
                if (!payloadObj.ContainsKey("to"))
                    payloadObj["to"] = JArray.FromObject(correosArray);

                string entidad = payloadObj["entidad"]?.ToString() ?? "(Entidad no especificada)";
                string representante = payloadObj["representanteLegal"]?.ToString() ?? "(Representante no especificado)";
                string numeroTramite = payloadObj["numeroTramite"]?.ToString() ?? "(N/A)";
                string linkConsulta = payloadObj["linkConsulta"]?.ToString() ?? "#";

                var attachments = new List<object>();

                // 📎 Adjuntar PDF
                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    _logger.LogInformation($"📎 Adjuntando archivo PDF: {pdfPath}");
                    // await Task.Delay(300); // Pequeña espera (puede no ser necesaria en producción)

                    byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);
                    string base64Pdf = Convert.ToBase64String(pdfBytes)
                        .Trim()
                        .Replace("\r", "")
                        .Replace("\n", "")
                        .Replace(" ", "");

                    // Validación previa para asegurar que es un Base64 válido
                    try { Convert.FromBase64String(base64Pdf); }
                    catch
                    {
                        _logger.LogError("❌ El contenido del PDF no es Base64 válido antes del envío.");
                        return false;
                    }

                    attachments.Add(new
                    {
                        fileName = Path.GetFileName(pdfPath),
                        contentType = "application/pdf",
                        contentBase64 = base64Pdf
                    });

                    _logger.LogInformation($"✅ PDF convertido a Base64 correctamente (longitud: {base64Pdf.Length} bytes).");
                }
                else
                {
                    _logger.LogWarning("⚠️ No se encontró el archivo PDF para adjuntar.");
                }

                // Determinar la lista de destinatarios final
                var toList = payloadObj["to"]?.ToObject<List<string>>() ?? correosArray;

                // Construir el cuerpo HTML por defecto (se reemplazará si se usa el bloque 7)
                var htmlBody = payloadObj["htmlBody"]?.ToString() ?? $@"
                <p>Doctor(a) {representante},</p>
                <p>La entidad <strong>{entidad}</strong> ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín, con el número del trámite <strong>{numeroTramite}</strong>.</p>
                <p>Puede consultar el estado del trámite en el siguiente link: 
                   <a href='{linkConsulta}'>{linkConsulta}</a></p>
                <p>Cordial saludo,<br/><br/>
 
                Departamento de Sistema de Seguro de Depósitos<br/>
                Fondo de Garantías de Instituciones Financieras – Fogafín<br/>
                PBX: 601 4321370 extensiones 255 - 142</p>";

                var emailBody = new
                {
                    to = toList,
                    subject = payloadObj["subject"]?.ToString() ?? $"Registro de entidad {entidad} - Trámite #{numeroTramite}",
                    htmlBody = htmlBody,
                    attachments = attachments // ← nunca null
                };

                string jsonBody = JsonConvert.SerializeObject(emailBody, Formatting.None);
                // Usar UTF8 con BOM para consistencia, aunque UTF8 sin BOM también es común
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("/api/send-email", content);
                string respContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Correo enviado correctamente mediante Azure Email Function.");
                    return true;
                }
                else
                {
                    _logger.LogError($"❌ Error al enviar correo. Código: {response.StatusCode}, Detalle: {respContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Excepción al intentar enviar correo mediante Azure Function.");
                return false;
            }
        }

    }

    // DTO de datos de la entidad
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
                // 1️⃣ Leer el body JSON
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

                // 2️⃣ Validar conexión SQL
                string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    var configError = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await configError.WriteStringAsync("No se encontró la cadena de conexión en Azure.");
                    return configError;
                }

                // 3️⃣ Verificar si el NIT ya existe
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string checkQuery = "SELECT COUNT(*) FROM dbo.TM02_ENTIDADFINANCIERA WHERE TM02_NIT = @Nit";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Nit", nit);
                        int count = (int)await checkCmd.ExecuteScalarAsync();
                        if (count > 0)
                        {
                            var existsResponse = req.CreateResponse(HttpStatusCode.Conflict);
                            await existsResponse.WriteStringAsync("Ya existe una entidad con el NIT suministrado.");
                            return existsResponse; // ❌ Detiene ejecución aquí
                        }
                    }
                }

                // 4️⃣ Extraer los datos del archivo (base64 o bytes)
                string? base64File = data?.archivoBase64;
                string? fileName = data?.nombreArchivo ?? $"{Guid.NewGuid()}.bin";
                string contentType = data?.contentType ?? "application/octet-stream";

                if (string.IsNullOrWhiteSpace(base64File))
                {
                    var badFile = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badFile.WriteStringAsync("Debe incluir el archivo codificado en base64 en el campo 'archivoBase64'.");
                    return badFile;
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(base64File);
                }
                catch
                {
                    var badBase64 = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badBase64.WriteStringAsync("El formato del archivo base64 no es válido.");
                    return badBase64;
                }

                // 5️⃣ Subir archivo al contenedor
                string storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
                string containerName = "adjuntos";

                var blobServiceClient = new BlobServiceClient(storageConnection);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                // 🔹 Normalizar nombre original
                string cleanName = Path.GetFileName(fileName)
                    .Replace(" ", "_")
                    .Replace(":", "_")
                    .Replace("/", "_");

                // 🔹 Generar nombre único estructurado
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
                _logger.LogError(ex, "Error al subir archivo al Blob Storage.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error interno del servidor: {ex.Message}");
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
            _logger.LogInformation($"🔍 Consultando existencia del NIT: {nit}");

            try
            {
                bool existe = await NitExisteAsync(nit);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    nit,
                    existe
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar el NIT en la base de datos.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error al consultar el NIT.");
                return errorResponse;
            }
        }

        // 🔹 Reutilizamos la función auxiliar
        private async Task<bool> NitExisteAsync(string nit)
        {
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("❌ No se encontró la cadena de conexión SqlConnectionString.");
                throw new InvalidOperationException("No se encontró la cadena de conexión.");
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT COUNT(*) 
                        FROM [TM02_ENTIDADFINANCIERA]
                        WHERE [TM02_NIT] = @Nit";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Nit", nit);
                        int count = (int)await cmd.ExecuteScalarAsync();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar existencia del NIT en la base de datos.");
                throw;
            }
        }
    }

    public class ConsultarSectores
    {
        private readonly ILogger _logger;

        // Constructor que inyecta el logger
        public ConsultarSectores(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarSectores>();
        }

        /// <summary>
        /// Endpoint que consulta los nombres de los sectores financieros dado un listado de códigos.
        /// Ruta de ejemplo: /api/ConsultarSectores/1,2,4,28
        /// </summary>
        [Function("ConsultarSectores")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ConsultarSectores/{codigos}")] HttpRequestData req,
            string codigos) // {codigos} contendrá la lista separada por comas
        {
            _logger.LogInformation($"📚 Consultando sectores con códigos: {codigos}");

            // 1. Validar y parsear los códigos
            if (string.IsNullOrWhiteSpace(codigos))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("La lista de códigos no puede estar vacía.");
                return badRequest;
            }

            // Convertir la cadena "1,2,4,28" en una lista de IDs para el SQL
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

            try
            {
                var sectores = await ObtenerSectoresAsync(listaCodigos);

                var response = req.CreateResponse(HttpStatusCode.OK);
                // Devuelve la lista de objetos de sector en formato JSON
                await response.WriteAsJsonAsync(sectores);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar los sectores en la base de datos.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error al consultar los sectores financieros.");
                return errorResponse;
            }
        }

        // 🔹 Función auxiliar para interactuar con la base de datos
        private async Task<List<SectorFinanciero>> ObtenerSectoresAsync(List<string> codigos)
        {
            var sectores = new List<SectorFinanciero>();
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("❌ No se encontró la cadena de conexión SqlConnectionString.");
                throw new InvalidOperationException("No se encontró la cadena de conexión.");
            }

            // Crear una lista de parámetros para evitar inyección SQL (mejora de seguridad)
            var parameters = new List<SqlParameter>();
            var sqlInClause = new List<string>();

            for (int i = 0; i < codigos.Count; i++)
            {
                // Crea un nombre de parámetro único, ej: @p0, @p1
                var paramName = $"@p{i}";
                sqlInClause.Add(paramName);
                parameters.Add(new SqlParameter(paramName, codigos[i]));
            }

            // Se construye el query con los placeholders para los parámetros
            string query = $@"
            SELECT 
                [TM01_CODIGO], 
                [TM01_NOMBRE]
            FROM [SIIR-ProdV1].[dbo].[TM01_SectorFinanciero]
            WHERE [TM01_CODIGO] IN ({string.Join(",", sqlInClause)})"; // E.g., IN (@p0, @p1, @p2)

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar sectores en la base de datos.");
                throw;
            }
        }
    }

    // 📌 Clase para modelar el resultado de la consulta SQL
    public class SectorFinanciero
    {
        // El TM01_CODIGO es el valor que se usará en el <option value="">
        [JsonPropertyName("TM01_CODIGO")]
        public string Codigo { get; set; } = string.Empty;

        // El TM01_NOMBRE es el texto que se mostrará al usuario
        [JsonPropertyName("TM01_NOMBRE")]
        public string Nombre { get; set; } = string.Empty;
    }
}
