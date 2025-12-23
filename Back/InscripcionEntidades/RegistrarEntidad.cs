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
                string pdfUrl = string.Empty;
                HttpResponseData okResponse;
                int tm02Codigo = 0;
                string numeroTramiteStr = string.Empty;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    if (await NitExisteAsync(data.Nit))
                    {
                        var existsResponse = req.CreateResponse(HttpStatusCode.Conflict);
                        await existsResponse.WriteStringAsync("Ya existe una entidad con el NIT suministrado.");
                        return existsResponse;
                    }

                    string getMaxCodeQuery = @"
                    SELECT ISNULL(MAX(TM02_CODIGO), 99899) + 1
                    FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA]
                    WHERE TM02_CODIGO >= 99900";
                    
                    using (SqlCommand cmdMaxCode = new SqlCommand(getMaxCodeQuery, conn))
                    {
                        object? result = await cmdMaxCode.ExecuteScalarAsync();
                        tm02Codigo = Convert.ToInt32(result);
                    }

                    string insertTM02 = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] 
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
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM08_ConsecutivoEnt] (TM08_TM01_Codigo, TM08_Ano)
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

                    var (pdfUrlTemp, localPath) = await GenerarYSubirResumenPdf(data, numeroTramite);
                    pdfUrl = pdfUrlTemp;
                    localPdfPath = localPath;

                    string updateTM02 = @"
                    UPDATE [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] SET
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
                        INSERT INTO [SIIR-ProdV1].[dbo].[TN07_Adjuntos] (TN07_TM02_Codigo, TN07_Archivo, TN07_Fecha)
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

                    _logger.LogWarning("✅ ADJUNTOS PROCESADOS - INICIANDO CORREOS");

                    // Insertar estado inicial en TN05_Historico_Estado
                    string insertEstado = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TN05_Historico_Estado]
                    (TN05_TM02_Tipo, TN05_TM02_Codigo, TN05_TM01_EstadoAnterior, TN05_TM01_EstadoActual, TN05_Fecha, TN05_TN03_Usuario, TN05_Observaciones)
                    VALUES (@TipoSector, @TM02Codigo, NULL, 12, @Fecha, @Usuario, @Observaciones)";

                    using (SqlCommand cmdEstado = new SqlCommand(insertEstado, conn))
                    {
                        cmdEstado.Parameters.AddWithValue("@TipoSector", data.TipoEntidad);
                        cmdEstado.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                        cmdEstado.Parameters.AddWithValue("@Fecha", DateTime.Now);
                        cmdEstado.Parameters.AddWithValue("@Usuario", "USUARIOWEB");
                        cmdEstado.Parameters.AddWithValue("@Observaciones", "Estado inicial - Entidad registrada");
                        await cmdEstado.ExecuteNonQueryAsync();
                    }

                    // Insertar pago por inscripción en TN06_Pagos
                    if (data.ValorPagado > 0)
                    {
                        int? comprobanteId = null;
                        
                        // Buscar el ID del comprobante en TN07_Adjuntos
                        if (!string.IsNullOrEmpty(data.RutaComprobantePago))
                        {
                            string buscarComprobante = @"
                            SELECT TN07_Id FROM [SIIR-ProdV1].[dbo].[TN07_Adjuntos] 
                            WHERE TN07_TM02_Codigo = @TM02Codigo AND TN07_Archivo = @Archivo";
                            
                            using (SqlCommand cmdBuscar = new SqlCommand(buscarComprobante, conn))
                            {
                                cmdBuscar.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                                cmdBuscar.Parameters.AddWithValue("@Archivo", data.RutaComprobantePago);
                                object? result = await cmdBuscar.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                {
                                    comprobanteId = Convert.ToInt32(result);
                                }
                            }
                        }

                        string insertPago = @"
                        INSERT INTO [SIIR-ProdV1].[dbo].[TN06_Pagos]
                        (TN06_TM02_Tipo, TN06_TM02_Codigo, TN06_Fecha, TN06_Valor, TN06_Comprobante)
                        VALUES (@TipoSector, @TM02Codigo, @FechaPago, @Valor, @Comprobante)";

                        using (SqlCommand cmdPago = new SqlCommand(insertPago, conn))
                        {
                            cmdPago.Parameters.AddWithValue("@TipoSector", data.TipoEntidad);
                            cmdPago.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                            cmdPago.Parameters.AddWithValue("@FechaPago", data.FechaPago ?? DateTime.Now);
                            cmdPago.Parameters.AddWithValue("@Valor", data.ValorPagado);
                            cmdPago.Parameters.AddWithValue("@Comprobante", (object?)comprobanteId ?? DBNull.Value);
                            await cmdPago.ExecuteNonQueryAsync();
                        }
                    }

                    // Obtener nombre del responsable asignado (Jefe SSD con acceso a SIEF)
                    string responsableQuery = @"
                    SELECT TOP 1 u.TM03_Nombre
                    FROM [SIIR-ProdV1].[dbo].[TM03_Usuario] u
                    INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] p 
                        ON u.TM03_Usuario COLLATE Latin1_General_CI_AS = p.TM15_TM04_Identificacion COLLATE Latin1_General_CI_AS
                    WHERE p.TM15_TM12_TM01_Codigo = '17' 
                        AND p.TM15_TM14_Perfil = 'Jefe SSD'
                    ORDER BY u.TM03_Nombre";

                    using (SqlCommand cmdResp = new SqlCommand(responsableQuery, conn))
                    {
                        object? respResult = await cmdResp.ExecuteScalarAsync();
                        if (respResult != null && respResult != DBNull.Value)
                        {
                            nombreResponsableAsignado = respResult.ToString()!;
                        }
                    }

                    // Preparar datos para envío de correo
                    string representanteLegal = data.NombreRep;
                    string entidadNombre = data.Nombre;
                    numeroTramiteStr = $"{consecutivo}{tipoCodigo}{currentYear}";
                    string linkConsulta = "https://sadevsiefexterno.z20.web.core.windows.net/local/pages/consulta.html";
                                           
                    _logger.LogWarning($"🚀 INICIANDO PROCESO DE CORREOS PARA ENTIDAD: {entidadNombre} - TRAMITE: {numeroTramiteStr}");
                    _logger.LogWarning("🔍 INICIANDO CONSULTA DE DESTINATARIOS...");
                    
                    // Consultar destinatarios por áreas específicas
                    List<string> correosArea = new();
                    var destinatariosPorArea = new Dictionary<string, List<(string nombre, string email)>>();
                    var siglasAreas = new Dictionary<string, string>();
                    
                    try
                    {
                        // Primero obtener las siglas de las áreas
                        string siglasQuery = @"
                        SELECT TM02_Codigo, TM02_Nombre 
                        FROM [SIIR-ProdV1].[dbo].[TM02_Area] 
                        WHERE TM02_Codigo IN (52060, 52070, 59030)";
                        
                        using (SqlCommand cmdSiglas = new SqlCommand(siglasQuery, conn))
                        {
                            using (SqlDataReader readerSiglas = await cmdSiglas.ExecuteReaderAsync())
                            {
                                while (await readerSiglas.ReadAsync())
                                {
                                    string codigo = readerSiglas["TM02_Codigo"].ToString() ?? "";
                                    string sigla = readerSiglas["TM02_Nombre"].ToString() ?? "";
                                    siglasAreas[codigo] = sigla;
                                }
                            }
                        }
                        
                        // Obtener destinatarios desde TM03_Usuario
                        string destinatariosQuery = @"
                        SELECT 
                            u.TM03_Nombre AS NombreCompleto,
                            u.TM03_Correo AS TM03_Email,
                            u.TM03_TM02_Codigo AS CodigoArea
                        FROM [SIIR-ProdV1].[dbo].[TM03_Usuario] u
                        WHERE u.TM03_TM02_Codigo IN (52060, 52070, 59030)
                        AND u.TM03_Correo IS NOT NULL
                        AND u.TM03_Correo != ''
                        ORDER BY u.TM03_TM02_Codigo, u.TM03_Nombre";

                        using (SqlCommand cmdDestinatarios = new SqlCommand(destinatariosQuery, conn))
                        {
                            using (SqlDataReader reader = await cmdDestinatarios.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string nombre = reader["NombreCompleto"].ToString() ?? "";
                                    string email = reader["TM03_Email"].ToString() ?? "";
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
                        
                        // Agregar fogafin@fogafin.gov.co a todas las áreas
                        foreach (var area in destinatariosPorArea.Keys.ToList())
                        {
                            destinatariosPorArea[area].Add(("Fogafín Institucional", "fogafin@fogafin.gov.co"));
                        }
                        correosArea.Add("fogafin@fogafin.gov.co");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ ERROR EN CONSULTA DE DESTINATARIOS");
                    }
                    
                    // Crear lista de correos de confirmación (todos los correos de la entidad)
                    var correosConfirmacion = new List<string>();
                    
                    // Agregar correos de la entidad
                    if (!string.IsNullOrEmpty(data.CorreoNoti))
                        correosConfirmacion.Add(data.CorreoNoti);
                    if (!string.IsNullOrEmpty(data.CorreoRep))
                        correosConfirmacion.Add(data.CorreoRep);
                    if (!string.IsNullOrEmpty(data.CorreoResponsable))
                        correosConfirmacion.Add(data.CorreoResponsable);
                    
                    // Agregar fogafin@fogafin.gov.co
                    correosConfirmacion.Add("fogafin@fogafin.gov.co");
                    
                    // Eliminar duplicados
                    correosConfirmacion = correosConfirmacion.Distinct().ToList();
                    
                    _logger.LogWarning("📧 CORREOS DE CONFIRMACIÓN:");
                    foreach (var correo in correosConfirmacion)
                    {
                        _logger.LogWarning($"  📧 {correo}");
                    }

                    // Filtrar correos para no enviar únicamente a fogafin@fogafin.gov.co
                    // var correosAreaFiltrados = correosArea.Where(email => !email.EndsWith("@fogafin.gov.co")).ToList();
                    // var correosConfirmacionFiltrados = correosConfirmacion.Where(email => !email.EndsWith("@fogafin.gov.co")).ToList();
                    var correosAreaFiltrados = correosArea.Where(email => email != "fogafin@fogafin.gov.co").ToList();
                    var correosConfirmacionFiltrados = correosConfirmacion.Where(email => email != "fogafin@fogafin.gov.co").ToList();
                    
                    _logger.LogWarning($"📧 CORREOS ÁREA: {string.Join(", ", correosAreaFiltrados)}");
                    _logger.LogWarning($"📧 CORREOS CONFIRMACIÓN: {string.Join(", ", correosConfirmacionFiltrados)}");

                    // Envío de correo al área responsable (sin adjunto)
                    if (correosAreaFiltrados.Any())
                    {
                        // Enviar correo específico por área
                        foreach (var area in destinatariosPorArea.Where(a => correosAreaFiltrados.Intersect(a.Value.Select(v => v.email)).Any()))
                        {
                            var correosAreaEspecifica = area.Value.Select(v => v.email).Where(email => correosAreaFiltrados.Contains(email)).ToList();
                            string subject = "";
                            string htmlBody = "";
                            
                            switch (area.Key)
                            {
                                case "52060": // DIF
                                    subject = "Revisión y Aprobación del Formato de Inscripción de Terceros";
                                    htmlBody = $@"
                                    <p>Estimados miembros del Departamento de Información Financiera:</p>
                                    <p>La entidad <strong>{entidadNombre}</strong> ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín.</p>
                                    <p>Les solicitamos adelantar los trámites pertinentes de revisión y aprobación de dicho formato, así como con la creación del tercero en el aplicativo Apoteosys.</p>
                                    <p>Cordial saludo,<br/><br/>
                                    Departamento de Sistema de Seguro de Depósitos<br/>
                                    Fondo de Garantías de Instituciones Financieras – Fogafín<br/>
                                    PBX: 601 4321370 extensiones 255 - 142</p>";
                                    break;
                                    
                                case "52070": // DGC
                                    subject = "Proceso de Inscripción al Sistema de Seguro de Depósitos";
                                    htmlBody = $@"
                                    <p>Departamento de Gestión de contenidos:</p>
                                    <p>La entidad <strong>{entidadNombre}</strong> ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín.</p>
                                    <p>Le solicitamos gestionar la creación de una PQRS en onbase con asignación a SSD.</p>
                                    <p>Cordial saludo,<br/><br/>
                                    Departamento de Sistema de Seguro de Depósitos<br/>
                                    Fondo de Garantías de Instituciones Financieras – Fogafín<br/>
                                    PBX: 601 4321370 extensiones 255 - 142</p>";
                                    break;
                                    
                                case "59030": // SSD
                                    subject = "Proceso de Inscripción al Sistema de Seguro de Depósitos";
                                    htmlBody = $@"
                                    <p>Departamento de Sistema de Seguro de Depósitos:</p>
                                    <p>La entidad <strong>{entidadNombre}</strong> ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín.</p>
                                    <p>Le solicitamos iniciar el proceso de validación de la información en el aplicativo y actualizar el estado correspondiente del proceso.</p>
                                    <p>Cordial saludo,<br/><br/>
                                    Departamento de Sistema de Seguro de Depósitos<br/>
                                    Fondo de Garantías de Instituciones Financieras – Fogafín<br/>
                                    PBX: 601 4321370 extensiones 255 - 142</p>";
                                    break;
                            }
                            
                            var attachments = new List<object>();
                            
                            // Adjuntar formato de inscripción a terceros para DIF
                            if (area.Key == "52060" && !string.IsNullOrEmpty(data.CartaSolicitud))
                            {
                                try
                                {
                                    var storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
                                    var blobServiceClient = new BlobServiceClient(storageConnection);
                                    var uri = new Uri(data.CartaSolicitud);
                                    var containerName = uri.Segments[1].TrimEnd('/');
                                    var blobName = string.Join("", uri.Segments.Skip(2));
                                    var blobClient = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
                                    
                                    using var stream = new MemoryStream();
                                    await blobClient.DownloadToAsync(stream);
                                    var fileBytes = stream.ToArray();
                                    var base64File = Convert.ToBase64String(fileBytes);
                                    
                                    var fileName = Path.GetFileName(new Uri(data.CartaSolicitud).LocalPath);
                                    attachments.Add(new { fileName = fileName, contentBase64 = base64File });
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error al descargar archivo para adjuntar al correo DIF");
                                }
                            }
                            
                            var emailAreaPayload = new
                            {
                                to = correosAreaEspecifica,
                                subject = subject,
                                htmlBody = htmlBody,
                                attachments = attachments
                            };
                            
                            bool correoAreaEnviado = await EnviarCorreoAsync(emailAreaPayload, null, tm02Codigo, numeroTramiteStr);
                            _logger.LogWarning($"📨 Correo a {area.Key} enviado: {correoAreaEnviado}");
                            if (!correoAreaEnviado)
                            {
                                _logger.LogError($"❌ ERROR: Falló envío de correo a área {area.Key}");
                            }
                        }
                    }

                    // Envío de correo de confirmación al usuario (con PDF adjunto)
                    if (correosConfirmacionFiltrados.Any())
                    {
                        var plantillaUsuario = $@"Doctor(a) {representanteLegal}:<br>
<br>
La entidad {entidadNombre} ha iniciado el proceso de inscripción al Sistema de Seguro de Depósitos de Fogafín, con el número del trámite No. {numeroTramiteStr}.<br>
<br>
Puede consultar el estado del trámite en el siguiente link https://sadevsiefexterno.z20.web.core.windows.net/local/pages/consulta.html.<br>
<br>
Cordial saludo,<br>
<br>
Departamento de Sistema de Seguro de Depósitos<br>
Fondo de Garantías de Instituciones Financieras – Fogafín<br>
PBX: 601 4321370 extensiones 255 - 142";
                        
                        var emailUsuarioPayload = new
                        {
                            to = correosConfirmacionFiltrados,
                            subject = "Proceso de Inscripción al Sistema de Seguro de Depósitos",
                            htmlBody = plantillaUsuario,
                            attachments = new List<object>()
                        };
                        
                        bool correoUsuarioEnviado = await EnviarCorreoAsync(emailUsuarioPayload, localPdfPath, tm02Codigo, numeroTramiteStr);
                        _logger.LogWarning($"📧 Correo de confirmación enviado: {correoUsuarioEnviado}");
                        if (!correoUsuarioEnviado)
                        {
                            _logger.LogError("❌ ERROR: Falló envío de correo de confirmación al usuario");
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

                    okResponse = req.CreateResponse(HttpStatusCode.OK);
                    okResponse.Headers.Add("Content-Type", "application/json");
                    await okResponse.WriteStringAsync(JsonConvert.SerializeObject(responseObj));

                    if (File.Exists(localPdfPath))
                    {
                        File.Delete(localPdfPath);
                    }

                }

                // Ejecutar sistema de notificaciones con delay
                await Task.Delay(1000); // Esperar 1 segundo
                await CrearNotificacionAsync(data.Nit, data.Nombre, data.TipoEntidad, pdfUrl, currentYear, connectionString);

                return okResponse;
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

            // Obtener el logo de Fogafín
            byte[]? logoBytes = null;
            
            try
            {
                // Buscar el logo en diferentes ubicaciones posibles
                string[] possiblePaths = {
                    Path.Combine(Directory.GetCurrentDirectory(), "assets", "images", "logo.jpg"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "images", "logo.jpg"),
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "assets", "images", "logo.jpg"),
                    "assets/images/logo.jpg",
                    "../assets/images/logo.jpg"
                };

                foreach (string logoPath in possiblePaths)
                {
                    if (File.Exists(logoPath))
                    {
                        logoBytes = await File.ReadAllBytesAsync(logoPath);
                        _logger.LogInformation($"Logo cargado desde: {logoPath}");
                        break;
                    }
                }
                
                if (logoBytes == null)
                {
                    _logger.LogWarning($"Logo no encontrado en ninguna de las rutas: {string.Join(", ", possiblePaths)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No se pudo cargar el logo: {ex.Message}");
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Column(header =>
                    {
                        // Logo de Fogafín
                        if (logoBytes != null)
                        {
                            header.Item().AlignLeft().Width(150).Height(40).Image(logoBytes);
                        }
                        else
                        {
                            header.Item().AlignLeft().Text("Fogafín")
                                .FontSize(16).Bold().FontColor("#0066CC");
                        }
                        
                        header.Item().PaddingTop(10).Text("Fondo de Garantías de Instituciones Financieras")
                            .FontSize(8).FontColor("#666666");
                    });

                    page.Content().PaddingTop(20).Column(content =>
                    {
                        // Información del destinatario
                        content.Item().PaddingBottom(15).Column(destinatario =>
                        {
                            destinatario.Item().Text($"Señor (a): {data.NombreRep}")
                                .FontSize(10).Bold();
                        });

                        // Texto introductorio
                        content.Item().PaddingBottom(20).Text(
                            "A continuación, encontrará la información incluida en el formulario de inscripción de su entidad. Recuerde " +
                            "que puede consultar el estado de su trámite en la página de Fogafín: www.fogafin.gov.co")
                            .FontSize(10).LineHeight(1.2f);

                        // Sección: Datos de la entidad
                        content.Item().PaddingBottom(15).Column(datosEntidad =>
                        {
                            datosEntidad.Item().Text("Datos de la entidad")
                                .FontSize(12).Bold().FontColor("#4CAF50");
                            
                            datosEntidad.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                // Razón social
                                table.Cell().Padding(5).Text("Razón social:").FontSize(9);
                                table.Cell().Padding(5).Text(data.Nombre).FontSize(9).Bold();

                                // NIT
                                table.Cell().Padding(5).Text("Nit:").FontSize(9);
                                table.Cell().Padding(5).Text(data.Nit).FontSize(9).Bold();

                                // Tipo de entidad
                                table.Cell().Padding(5).Text("Tipo de entidad:").FontSize(9);
                                table.Cell().Padding(5).Text(ObtenerTipoEntidad(data.TipoEntidad)).FontSize(9).Bold();

                                // Fecha de resolución de constitución
                                table.Cell().Padding(5).Text("Fecha de resolución de constitución:").FontSize(9);
                                table.Cell().Padding(5).Text(data.FechaConstitucion.ToString("dd/MMMM/yyyy")).FontSize(9).Bold();

                                // Capital suscrito
                                table.Cell().Padding(5).Text("Capital suscrito:").FontSize(9);
                                table.Cell().Padding(5).Text($"$ {data.CapitalSuscrito:N2}").FontSize(9).Bold();

                                // Página web
                                table.Cell().Padding(5).Text("Página web:").FontSize(9);
                                table.Cell().Padding(5).Text(data.PaginaWeb ?? "N/A").FontSize(9).Bold();

                                // Correo de notificación
                                table.Cell().Padding(5).Text("Correo de notificación:").FontSize(9);
                                table.Cell().Padding(5).Text(data.CorreoNoti).FontSize(9).Bold();
                            });
                        });

                        // Sección: Información del pago de la inscripción
                        content.Item().PaddingBottom(15).Column(infoPago =>
                        {
                            infoPago.Item().Text("Información del pago de la inscripción")
                                .FontSize(12).Bold().FontColor("#4CAF50");
                            
                            infoPago.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                // Valor pagado por inscripción
                                table.Cell().Padding(5).Text("Valor pagado por inscripción:").FontSize(9);
                                table.Cell().Padding(5).Text($"$ {data.ValorPagado:N2}").FontSize(9).Bold();

                                // Fecha de pago de la inscripción
                                table.Cell().Padding(5).Text("Fecha de pago de la inscripción:").FontSize(9);
                                table.Cell().Padding(5).Text(data.FechaPago?.ToString("dd/MMMM/yyyy") ?? "N/A").FontSize(9).Bold();
                            });
                        });

                        // Sección: Información del Representante legal o apoderado
                        content.Item().PaddingBottom(15).Column(infoRep =>
                        {
                            infoRep.Item().Text("Información del Representante legal o apoderado")
                                .FontSize(12).Bold().FontColor("#4CAF50");
                            
                            infoRep.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                // Nombre(s) y Apellido(s)
                                table.Cell().Padding(5).Text("Nombre(s) y Apellido(s):").FontSize(9);
                                table.Cell().Padding(5).Text(data.NombreRep).FontSize(9).Bold();

                                // Tipo de identificación
                                table.Cell().Padding(5).Text("Tipo de identificación:").FontSize(9);
                                table.Cell().Padding(5).Text(ObtenerTipoDocumento(data.TipoDoc)).FontSize(9).Bold();

                                // Número de documento
                                table.Cell().Padding(5).Text("Número de documento:").FontSize(9);
                                table.Cell().Padding(5).Text(data.IdentificacionRep.ToString()).FontSize(9).Bold();

                                // Correo electrónico
                                table.Cell().Padding(5).Text("Correo electrónico:").FontSize(9);
                                table.Cell().Padding(5).Text(data.CorreoRep).FontSize(9).Bold();

                                // Teléfono
                                table.Cell().Padding(5).Text("Teléfono:").FontSize(9);
                                table.Cell().Padding(5).Text(data.TelefonoRep ?? "N/A").FontSize(9).Bold();

                                // Cargo
                                table.Cell().Padding(5).Text("Cargo:").FontSize(9);
                                table.Cell().Padding(5).Text(data.CargoRep).FontSize(9).Bold();
                            });
                        });

                        // Información adicional
                        content.Item().PaddingTop(20).Column(adicional =>
                        {
                            adicional.Item().Text("Cordialmente,")
                                .FontSize(10).Bold();
                            
                            adicional.Item().PaddingTop(10).Text("Fondo de Garantías de Instituciones Financieras")
                                .FontSize(9).Italic();
                        });
                    });

                    page.Footer().AlignCenter().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor("#999999");
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

        private string ObtenerTipoEntidad(int tipoEntidad)
        {
            return tipoEntidad switch
            {
                1 => "Bancos",
                2 => "Corporaciones Financieras",
                3 => "Compañías de Financiamiento",
                4 => "Cooperativas Financieras",
                _ => "Otro"
            };
        }

        private string ObtenerTipoDocumento(string tipoDoc)
        {
            return tipoDoc switch
            {
                "1" => "Cédula de ciudadanía",
                "2" => "Cédula de extranjería",
                "5" => "Pasaporte",
                "CC" => "Cédula de ciudadanía",
                "CE" => "Cédula de extranjería",
                "PA" => "Pasaporte",
                _ => tipoDoc
            };
        }

        private async Task<bool> EnviarCorreoAsync(object payload, string? pdfPath = null, int? tm02Codigo = null, string? numeroTramite = null)
        {
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            try
            {
                // Microservicio centralizado de correo
                string baseUrl = Environment.GetEnvironmentVariable("CENTRALIZED_EMAIL_URL") ?? "https://app-correo-centralizado-dev-bfbrcqgdgfbbaxhq.eastus2-01.azurewebsites.net";
                string apiKey = Environment.GetEnvironmentVariable("CENTRALIZED_EMAIL_API_KEY") ?? "envioCorreo-2025";

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var payloadObj = JObject.FromObject(payload);
                var correosArray = payloadObj["correosArea"]?.ToObject<List<string>>() ?? new List<string>();
                if (!payloadObj.ContainsKey("to"))
                    payloadObj["to"] = JArray.FromObject(correosArray);

                // Obtener adjuntos existentes del payload
                var attachments = payloadObj["attachments"]?.ToObject<List<object>>() ?? new List<object>();

                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);
                    string base64Pdf = Convert.ToBase64String(pdfBytes);

                    attachments.Add(new
                    {
                        fileName = Path.GetFileName(pdfPath),
                        contentBase64 = base64Pdf
                    });
                }

                var toList = payloadObj["to"]?.ToObject<List<string>>() ?? correosArray;
                string subject = payloadObj["subject"]?.ToString() ?? "Registro de entidad";
                string htmlBody = payloadObj["htmlBody"]?.ToString() ?? "<p>Registro completado</p>";
                string destinatarios = string.Join(", ", toList);

                // Formato del microservicio centralizado
                var emailBody = new
                {
                    to = toList,
                    subject = subject,
                    htmlBody = htmlBody,
                    textBody = "Registro de entidad financiera completado",
                    priority = "normal",
                    attachments = attachments
                };

                string jsonBody = JsonConvert.SerializeObject(emailBody, Formatting.None);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("/api/email/send", content);
                string respContent = await response.Content.ReadAsStringAsync();
                bool exitoso = response.IsSuccessStatusCode;
                
                if (!exitoso)
                {
                    _logger.LogError($"❌ API CORREO ERROR: Status={response.StatusCode}, Body={respContent}");
                }
                else
                {
                    _logger.LogInformation("✅ Correo enviado correctamente mediante Azure Email Function.");
                }

                // Guardar log en TM80_LOG_CORREOS
                if (!string.IsNullOrEmpty(connectionString) && tm02Codigo.HasValue)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            await conn.OpenAsync();
                            string insertLogCorreo = @"
                            INSERT INTO [SIIR-ProdV1].[dbo].[TM80_LOG_CORREOS]
                            (TM80_TM02_CODIGO, TM80_NUMERO_TRAMITE, TM80_DESTINATARIOS, TM80_ASUNTO, TM80_CUERPO, TM80_TIPO_CORREO, TM80_ESTADO_ENVIO, TM80_FECHA_ENVIO, TM80_USUARIO, TM80_ERROR_DETALLE)
                            VALUES (@TM02Codigo, @NumeroTramite, @Destinatarios, @Asunto, @Cuerpo, @TipoCorreo, @EstadoEnvio, @FechaEnvio, @Usuario, @ErrorDetalle)";
                            
                            using (SqlCommand cmdLog = new SqlCommand(insertLogCorreo, conn))
                            {
                                cmdLog.Parameters.AddWithValue("@TM02Codigo", tm02Codigo.Value);
                                cmdLog.Parameters.AddWithValue("@NumeroTramite", numeroTramite ?? "");
                                cmdLog.Parameters.AddWithValue("@Destinatarios", destinatarios);
                                cmdLog.Parameters.AddWithValue("@Asunto", subject);
                                cmdLog.Parameters.AddWithValue("@Cuerpo", htmlBody.Length > 4000 ? htmlBody.Substring(0, 4000) : htmlBody);
                                cmdLog.Parameters.AddWithValue("@TipoCorreo", "SIEF_INSCRIPCION");
                                cmdLog.Parameters.AddWithValue("@EstadoEnvio", exitoso ? "ENVIADO" : "ERROR");
                                cmdLog.Parameters.AddWithValue("@FechaEnvio", DateTime.Now);
                                cmdLog.Parameters.AddWithValue("@Usuario", "USUARIOWEB");
                                cmdLog.Parameters.AddWithValue("@ErrorDetalle", exitoso ? (object)DBNull.Value : response.ReasonPhrase ?? "Error desconocido");
                                await cmdLog.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogError(logEx, "Error al guardar log de correo en TM80");
                    }
                }

                return exitoso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo.");
                
                // Guardar log de error
                if (!string.IsNullOrEmpty(connectionString) && tm02Codigo.HasValue)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            await conn.OpenAsync();
                            string insertLogError = @"
                            INSERT INTO [SIIR-ProdV1].[dbo].[TM80_LOG_CORREOS]
                            (TM80_TM02_CODIGO, TM80_NUMERO_TRAMITE, TM80_DESTINATARIOS, TM80_ASUNTO, TM80_TIPO_CORREO, TM80_ESTADO_ENVIO, TM80_FECHA_ENVIO, TM80_USUARIO, TM80_ERROR_DETALLE)
                            VALUES (@TM02Codigo, @NumeroTramite, @Destinatarios, @Asunto, @TipoCorreo, @EstadoEnvio, @FechaEnvio, @Usuario, @ErrorDetalle)";
                            
                            using (SqlCommand cmdLog = new SqlCommand(insertLogError, conn))
                            {
                                cmdLog.Parameters.AddWithValue("@TM02Codigo", tm02Codigo.Value);
                                cmdLog.Parameters.AddWithValue("@NumeroTramite", numeroTramite ?? "");
                                cmdLog.Parameters.AddWithValue("@Destinatarios", "Error al procesar");
                                cmdLog.Parameters.AddWithValue("@Asunto", "Error en envío");
                                cmdLog.Parameters.AddWithValue("@TipoCorreo", "SIEF_INSCRIPCION");
                                cmdLog.Parameters.AddWithValue("@EstadoEnvio", "ERROR");
                                cmdLog.Parameters.AddWithValue("@FechaEnvio", DateTime.Now);
                                cmdLog.Parameters.AddWithValue("@Usuario", "USUARIOWEB");
                                cmdLog.Parameters.AddWithValue("@ErrorDetalle", ex.Message);
                                await cmdLog.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogError(logEx, "Error al guardar log de error de correo en TM80");
                    }
                }
                
                return false;
            }
        }

        private async Task CrearNotificacionAsync(string nit, string nombreEntidad, int tipoEntidad, string pdfUrl, int currentYear, string connectionString)
        {
            try
            {
                
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    
                    // Buscar el TM02_CODIGO y TM02_TM01_CODIGO por NIT
                    string buscarCodigoQuery = @"
                    SELECT TM02_CODIGO, TM02_TM01_CODIGO FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] 
                    WHERE TM02_NIT = @Nit";
                    
                    int tm02Codigo;
                    int tm02TM01Codigo;
                    using (SqlCommand cmdBuscar = new SqlCommand(buscarCodigoQuery, conn))
                    {
                        cmdBuscar.Parameters.AddWithValue("@Nit", nit);
                        using (SqlDataReader reader = await cmdBuscar.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                tm02Codigo = Convert.ToInt32(reader["TM02_CODIGO"]);
                                tm02TM01Codigo = reader["TM02_TM01_CODIGO"] == DBNull.Value ? 12 : Convert.ToInt32(reader["TM02_TM01_CODIGO"]);
                                _logger.LogWarning($"Entidad encontrada: TM02_CODIGO = {tm02Codigo}, TM02_TM01_CODIGO = {tm02TM01Codigo}");
                            }
                            else
                            {
                                _logger.LogWarning($"No se encontró entidad con NIT {nit}");
                                return;
                            }
                        }
                    }
                    
                    // Verificar que el código existe antes de insertar
                    string verificarQuery = @"
                    SELECT COUNT(*) FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] 
                    WHERE TM02_CODIGO = @Codigo";
                    
                    using (SqlCommand cmdVerificar = new SqlCommand(verificarQuery, conn))
                    {
                        cmdVerificar.Parameters.AddWithValue("@Codigo", tm02Codigo);
                        int count = Convert.ToInt32(await cmdVerificar.ExecuteScalarAsync());
                        _logger.LogWarning($"Verificación: TM02_CODIGO {tm02Codigo} existe {count} veces");
                        if (count == 0)
                        {
                            _logger.LogWarning($"TM02_CODIGO {tm02Codigo} no existe en la tabla");
                            return;
                        }
                    }
                    
                    // Verificar en qué tabla TM01 existe el código
                    string verificarSectorQuery = @"
                    SELECT COUNT(*) FROM [SIIR-ProdV1].[dbo].[TM01_SectorFinanciero] 
                    WHERE TM01_CODIGO = @TM01Codigo";
                    
                    string verificarEstadoQuery = @"
                    SELECT COUNT(*) FROM [SIIR-ProdV1].[dbo].[TM01_Estado] 
                    WHERE TM01_Codigo = @TM01Codigo";
                    
                    using (SqlCommand cmdSector = new SqlCommand(verificarSectorQuery, conn))
                    {
                        cmdSector.Parameters.AddWithValue("@TM01Codigo", tipoEntidad);
                        int countSector = Convert.ToInt32(await cmdSector.ExecuteScalarAsync());
                        _logger.LogWarning($"TM01_CODIGO {tipoEntidad} en TM01_SectorFinanciero: {countSector} veces");
                    }
                    
                    using (SqlCommand cmdEstado = new SqlCommand(verificarEstadoQuery, conn))
                    {
                        cmdEstado.Parameters.AddWithValue("@TM01Codigo", tipoEntidad);
                        int countEstado = Convert.ToInt32(await cmdEstado.ExecuteScalarAsync());
                        _logger.LogWarning($"TM01_Codigo {tipoEntidad} en TM01_Estado: {countEstado} veces");
                    }
                    
                    int currentTrimestre = (DateTime.Now.Month - 1) / 3 + 1;
                    
                    // Insertar en TM61_ENTIDADES_NOTIFICACION
                    _logger.LogWarning($"Insertando TM61: Anio={currentYear}, Trimestre={currentTrimestre}, TM02_Codigo={tm02Codigo}, TM01_Codigo={tipoEntidad}, TM62_Codigo=1");
                    
                    string insertNotificacion = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM61_ENTIDADES_NOTIFICACION] 
                    (TM61_Anio, TM61_Trimestre, TM61_TM02_Codigo, TM61_TM01_Codigo, TM61_TM62_Codigo, TM61_Fecha_Ingreso)
                    OUTPUT INSERTED.TM61_Codigo
                    VALUES (@Anio, @Trimestre, @TM02Codigo, @TM01Codigo, 1, @FechaIngreso)";
                    
                    int tm61Codigo;
                    using (SqlCommand cmdNotif = new SqlCommand(insertNotificacion, conn))
                    {
                        cmdNotif.Parameters.AddWithValue("@Anio", currentYear);
                        cmdNotif.Parameters.AddWithValue("@Trimestre", currentTrimestre);
                        cmdNotif.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                        cmdNotif.Parameters.AddWithValue("@TM01Codigo", tm02TM01Codigo);
                        cmdNotif.Parameters.AddWithValue("@FechaIngreso", DateTime.Now);
                        
                        try {
                            tm61Codigo = Convert.ToInt32(await cmdNotif.ExecuteScalarAsync());
                            _logger.LogWarning($"TM61 insertado exitosamente con código: {tm61Codigo}");
                        } catch (Exception ex) {
                            _logger.LogError($"Error en INSERT TM61: {ex.Message}");
                            throw;
                        }
                    }
                    
                    // Insertar en TM63_DOCUMENTOS_NOTIFICACION con código 42 (SIEF: Inscripción registrada)
                    string insertDocumento = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM63_DOCUMENTOS_NOTIFICACION]
                    (TM63_TM61_Codigo, TM63_TM59_Codigo, TM63_Texto, TM63_Ruta_Generado, TM63_Fecha_Creacion, TM63_Usuario_Creacion)
                    VALUES (@TM61Codigo, 42, @Texto, @RutaGenerado, @FechaCreacion, @UsuarioCreacion)";
                    
                    using (SqlCommand cmdDoc = new SqlCommand(insertDocumento, conn))
                    {
                        cmdDoc.Parameters.AddWithValue("@TM61Codigo", tm61Codigo);
                        cmdDoc.Parameters.AddWithValue("@Texto", $"Resumen de inscripción - {nombreEntidad}");
                        cmdDoc.Parameters.AddWithValue("@RutaGenerado", pdfUrl);
                        cmdDoc.Parameters.AddWithValue("@FechaCreacion", DateTime.Now);
                        cmdDoc.Parameters.AddWithValue("@UsuarioCreacion", "USUARIOWEB");
                        await cmdDoc.ExecuteNonQueryAsync();
                    }
                    
                    // Insertar en TM64_LOG_NOTIFICACION
                    string insertLog = @"
                    INSERT INTO [SIIR-ProdV1].[dbo].[TM64_LOG_NOTIFICACION]
                    (TM64_Anio, TM64_Trimestre, TM64_TM61_Codigo, TM64_TM02_Codigo, TM64_TM01_Codigo, TM64_TM62_Codigo_Ant, TM64_TM62_Codigo_Act, TM64_Usuario, TM64_Descripción_Accion, TM64_Fecha_Accion)
                    VALUES (@Anio, @Trimestre, @TM61Codigo, @TM02Codigo, @TM01Codigo, NULL, 1, @Usuario, @Descripcion, @FechaAccion)";
                    
                    using (SqlCommand cmdLog = new SqlCommand(insertLog, conn))
                    {
                        cmdLog.Parameters.AddWithValue("@Anio", currentYear);
                        cmdLog.Parameters.AddWithValue("@Trimestre", currentTrimestre);
                        cmdLog.Parameters.AddWithValue("@TM61Codigo", tm61Codigo);
                        cmdLog.Parameters.AddWithValue("@TM02Codigo", tm02Codigo);
                        cmdLog.Parameters.AddWithValue("@TM01Codigo", tm02TM01Codigo);
                        cmdLog.Parameters.AddWithValue("@Usuario", "USUARIOWEB");
                        cmdLog.Parameters.AddWithValue("@Descripcion", "Registro inicial de entidad - Estado GENERADO");
                        cmdLog.Parameters.AddWithValue("@FechaAccion", DateTime.Now);
                        await cmdLog.ExecuteNonQueryAsync();
                    }
                    
                    _logger.LogInformation($"✅ Sistema de notificaciones: Registro creado TM61_Codigo={tm61Codigo}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear registro en sistema de notificaciones (asíncrono)");
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
