using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ConsultarDetalleEntidad
    {
        private readonly ILogger _logger;

        public ConsultarDetalleEntidad(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ConsultarDetalleEntidad>();
        }

        [Function("ConsultarDetalleEntidad")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ConsultarDetalleEntidad/{entidadId}")] HttpRequestData req,
            string entidadId)
        {
            _logger.LogInformation($"Consultando detalle de la entidad: {entidadId}");

            try
            {
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            e.TM02_CODIGO,
                            e.TM02_NOMBRE,
                            e.TM02_NIT,
                            e.TM02_Correo_Noti,
                            e.TM02_PaginaWeb,
                            e.TM02_FECHAINSCRIPCION,
                            e.TM02_Nombre_Rep,
                            e.TM02_Identificacion_Rep,
                            e.TM02_Correo_Rep,
                            e.TM02_Telefono_Rep,
                            e.TM02_Cargo_Rep,
                            e.TM02_NombreResponsable,
                            e.TM02_CorreoResponsable,
                            e.TM02_TelefonoResponsable,
                            e.TM02_FechaConstitucion,
                            e.TM02_CapitalSuscrito,
                            e.TM02_ValorPagado,
                            e.TM02_FechaPago,
                            e.TM02_RutaComprobantePago,
                            e.TM02_CertificadoSuper,
                            s.TM01_NOMBRE as TipoEntidad,
                            est.TM01_Nombre as EstadoTramite,
                            h.TN05_TM01_EstadoActual as EstadoId,
                            CONCAT(c.TM08_Consecutivo, c.TM08_TM01_Codigo, c.TM08_Ano) as NumeroTramite
                        FROM [SIIR-ProdV1].[dbo].[TM02_ENTIDADFINANCIERA] e
                        LEFT JOIN [SIIR-ProdV1].[dbo].[TM01_SECTORFINANCIERO] s ON e.TM02_TM01_CodigoSectorF = s.TM01_CODIGO
                        LEFT JOIN [SIIR-ProdV1].[dbo].[TM08_ConsecutivoEnt] c ON e.TM02_TM08_Consecutivo = c.TM08_Consecutivo
                        LEFT JOIN (
                            SELECT TN05_TM02_Codigo, TN05_TM01_EstadoActual,
                                   ROW_NUMBER() OVER (PARTITION BY TN05_TM02_Codigo ORDER BY TN05_Fecha DESC) as rn
                            FROM [SIIR-ProdV1].[dbo].[TN05_Historico_Estado]
                            WHERE TN05_TM02_Tipo = 1
                        ) h ON e.TM02_CODIGO = h.TN05_TM02_Codigo AND h.rn = 1
                        LEFT JOIN [SIIR-ProdV1].[dbo].[TM01_Estado] est ON h.TN05_TM01_EstadoActual = est.TM01_Codigo
                        WHERE e.TM02_CODIGO = @entidadId";

                    string queryAdjuntos = @"
                        SELECT TN07_Id, TN07_Archivo 
                        FROM [SIIR-ProdV1].[dbo].[TN07_Adjuntos] 
                        WHERE TN07_TM02_Codigo = @entidadId";
                        
                    string queryPagos = @"
                        SELECT TN06_Fecha, TN06_Valor, TN06_Comprobante
                        FROM [SIIR-ProdV1].[dbo].[TN06_Pagos] 
                        WHERE TN06_TM02_Codigo = @entidadId
                        ORDER BY TN06_Fecha";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@entidadId", entidadId);

                        string tipoEntidad = "", nit = "", correoNotificacion = "", paginaWeb = "", numeroTramite = "", fechaInscripcion = "", estadoTramite = "", estadoId = "", nombreRepresentante = "", identificacionRepresentante = "", correoRepresentante = "", telefonoRepresentante = "", cargoRepresentante = "", nombreResponsableRegistro = "", correoResponsableRegistro = "", telefonoResponsableRegistro = "", fechaConstitucion = "", capitalSuscrito = "", valorPagado = "", fechaPago = "", rutaComprobantePago = "", certificadoSuper = "";
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                tipoEntidad = reader["TipoEntidad"]?.ToString() ?? "";
                                nit = reader["TM02_NIT"]?.ToString() ?? "";
                                correoNotificacion = reader["TM02_Correo_Noti"]?.ToString() ?? "";
                                paginaWeb = reader["TM02_PaginaWeb"]?.ToString() ?? "";
                                numeroTramite = reader["NumeroTramite"]?.ToString() ?? "";
                                fechaInscripcion = reader["TM02_FECHAINSCRIPCION"]?.ToString() ?? "";
                                estadoTramite = reader["EstadoTramite"]?.ToString() ?? "";
                                estadoId = reader["EstadoId"]?.ToString() ?? "";
                                nombreRepresentante = reader["TM02_Nombre_Rep"]?.ToString() ?? "";
                                identificacionRepresentante = reader["TM02_Identificacion_Rep"]?.ToString() ?? "";
                                correoRepresentante = reader["TM02_Correo_Rep"]?.ToString() ?? "";
                                telefonoRepresentante = reader["TM02_Telefono_Rep"]?.ToString() ?? "";
                                cargoRepresentante = reader["TM02_Cargo_Rep"]?.ToString() ?? "";
                                nombreResponsableRegistro = reader["TM02_NombreResponsable"]?.ToString() ?? "";
                                correoResponsableRegistro = reader["TM02_CorreoResponsable"]?.ToString() ?? "";
                                telefonoResponsableRegistro = reader["TM02_TelefonoResponsable"]?.ToString() ?? "";
                                fechaConstitucion = reader["TM02_FechaConstitucion"]?.ToString() ?? "";
                                capitalSuscrito = reader["TM02_CapitalSuscrito"]?.ToString() ?? "";
                                valorPagado = reader["TM02_ValorPagado"]?.ToString() ?? "";
                                fechaPago = reader["TM02_FechaPago"]?.ToString() ?? "";
                                rutaComprobantePago = reader["TM02_RutaComprobantePago"]?.ToString() ?? "";
                                certificadoSuper = reader["TM02_CertificadoSuper"]?.ToString() ?? "";
                            }
                            else
                            {
                                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                                await notFoundResponse.WriteStringAsync("Entidad no encontrada");
                                return notFoundResponse;
                            }
                        }

                        // Obtener archivos adjuntos
                        var archivos = new List<string>();
                        var archivosConId = new List<object>();
                        using (var commandAdjuntos = new SqlCommand(queryAdjuntos, connection))
                        {
                            commandAdjuntos.Parameters.AddWithValue("@entidadId", entidadId);
                            using (var readerAdjuntos = await commandAdjuntos.ExecuteReaderAsync())
                            {
                                while (await readerAdjuntos.ReadAsync())
                                {
                                    var url = readerAdjuntos["TN07_Archivo"]?.ToString() ?? "";
                                    var id = readerAdjuntos["TN07_Id"]?.ToString() ?? "";
                                    archivos.Add(url);
                                    archivosConId.Add(new { id, url });
                                }
                            }
                        }
                        
                        // Agregar certificado de superintendencia si existe
                        if (!string.IsNullOrEmpty(certificadoSuper))
                        {
                            archivos.Add($"RESOLUCION_{certificadoSuper}");
                        }
                        
                        // Obtener pagos
                        var pagos = new List<object>();
                        using (var commandPagos = new SqlCommand(queryPagos, connection))
                        {
                            commandPagos.Parameters.AddWithValue("@entidadId", entidadId);
                            using (var readerPagos = await commandPagos.ExecuteReaderAsync())
                            {
                                while (await readerPagos.ReadAsync())
                                {
                                    pagos.Add(new {
                                        TN06_Fecha = readerPagos["TN06_Fecha"]?.ToString() ?? "",
                                        TN06_Valor = readerPagos["TN06_Valor"]?.ToString() ?? "",
                                        TN06_Comprobante = readerPagos["TN06_Comprobante"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }

                        var detalle = new
                        {
                            tipoEntidad,
                            nit,
                            correoNotificacion,
                            paginaWeb,
                            numeroTramite,
                            fechaInscripcion,
                            estadoNombre = estadoTramite,
                            estadoId = !string.IsNullOrEmpty(estadoId) ? int.Parse(estadoId) : 0,
                            nombreRepresentante,
                            identificacionRepresentante,
                            correoRepresentante,
                            telefonoRepresentante,
                            cargoRepresentante,
                            nombreResponsableRegistro,
                            correoResponsableRegistro,
                            telefonoResponsableRegistro,
                            fechaConstitucion,
                            capitalSuscrito,
                            valorPagado,
                            fechaPago,
                            rutaComprobantePago,
                            archivos,
                            archivosConId,
                            pagos
                        };

                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(detalle));
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar detalle de entidad");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }
    }
}