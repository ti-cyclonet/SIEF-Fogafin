using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

// Necesitas definir un DTO para la respuesta, similar al que usamos para la interfaz pública
public class EstadoTramiteDto
{
    public string NombreEntidad { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string NumeroTramite { get; set; } = string.Empty;
    public string EstadoTramite { get; set; } = string.Empty;
    public string NombreResponsable { get; set; } = string.Empty;
    public string CargoResponsable { get; set; } = string.Empty;
}

public class ConsultarEstadoTramite
{
    private readonly ILogger _logger;

    // Inyección del logger similar a RegistrarEntidad
    public ConsultarEstadoTramite(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ConsultarEstadoTramite>();
    }

    // Usaremos un HTTP GET con los parámetros en la ruta.
    [Function("ConsultarEstadoTramite")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "estado-tramite/{nit}/{numeroTramite}")] HttpRequestData req,
        string nit,
        string numeroTramite)
    {
        _logger.LogInformation($"🔍 Solicitud para consultar estado del trámite. NIT: {nit}, Trámite: {numeroTramite}");

        // 1. Obtener la cadena de conexión
        string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("❌ No se encontró la cadena de conexión en Azure.");
            var configError = req.CreateResponse(HttpStatusCode.InternalServerError);
            await configError.WriteStringAsync("Error de configuración de la conexión a la base de datos.");
            return configError;
        }

        // 2. Desglosar el número de trámite para la consulta (consecutivo, código de tipo)
        // Se asume que el formato de númeroTramite es: {consecutivo}{tipoCodigo}{año}
        // Este ejemplo se simplifica asumiendo una longitud de 9 o 10 caracteres (ej: 20251025-1 -> 202510251)

        // El número de trámite almacenado en el registro es:
        // TN04_TM08_Consecutivo (int)
        // TN04_TM01_CodigoSectorF (int, que corresponde al TipoEntidad)

        // Para simplificar la consulta pública, podemos asumir que se pasa el valor completo del trámite (ej: 20251025-1)
        // Y el NIT debe coincidir.

        // Para este ejemplo, usaremos el NIT y reconstruiremos el número de trámite esperado

        // Extraemos el consecutivo, tipo, y año del número de trámite
        if (!int.TryParse(numeroTramite.Substring(0, numeroTramite.Length - 5), out int consecutivo))
        {
            _logger.LogWarning($"Formato de número de trámite inválido: {numeroTramite}");
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("El formato del número de trámite es inválido.");
            return badResponse;
        }

        // 3. Lógica Principal de Consulta
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 🔹 Consulta que une TN04_Entidad con TM02_ENTIDADFINANCIERA y TM01_Estado
                // y TM03_Usuario para obtener el estado y el responsable.
                string query = @"
                SELECT TOP 1 
                    TM2.TM02_NOMBRE AS NombreEntidad,
                    TM2.TM02_NIT AS Nit,
                    CAST(TM2.TM02_TM08_Consecutivo AS VARCHAR) + 
                    CAST(TM2.TM02_TM01_CodigoSectorF AS VARCHAR) + 
                    CAST(YEAR(TM2.TM02_Fecha) AS VARCHAR) AS NumeroTramiteCalculado,
                    TM1.TM01_Nombre AS EstadoTramite,
                    TM2.TM02_NombreResponsable AS NombreResponsable,
                    TM2.TM02_TelefonoResponsable AS CargoResponsable
                FROM 
                    dbo.TM02_ENTIDADFINANCIERA TM2
                INNER JOIN 
                    dbo.TM01_Estado TM1 ON TM2.TM02_TM01_CODIGO = TM1.TM01_Codigo
                WHERE 
                    TM2.TM02_NIT = @Nit AND 
                    TM2.TM02_TM08_Consecutivo = @Consecutivo
                ORDER BY 
                    TM2.TM02_Fecha DESC;"; // Obtener el registro más reciente si hay varios

                EstadoTramiteDto? resultData = null;

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Nit", nit);
                    // Usaremos solo el consecutivo para hacer la búsqueda más robusta y evitamos el riesgo de formato de string.
                    cmd.Parameters.AddWithValue("@Consecutivo", consecutivo);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            resultData = new EstadoTramiteDto
                            {
                                NombreEntidad = reader["NombreEntidad"].ToString()!,
                                Nit = reader["Nit"].ToString()!,
                                // El número de trámite devuelto es el valor correcto (consecutivo + tipo + año)
                                NumeroTramite = reader["NumeroTramiteCalculado"].ToString()!,
                                EstadoTramite = reader["EstadoTramite"].ToString()!,
                                NombreResponsable = reader["NombreResponsable"].ToString()!,
                                // En la vista de ejemplo, el cargo es 'Experto - SSD', si no hay un campo específico, usamos el disponible más cercano o un valor por defecto. 
                                // Para el mock de la interfaz usaremos 'Experto - SSD' ya que TN04_TelefonoResponsable no es un cargo.
                                CargoResponsable = "Experto - SSD"
                            };
                        }
                    }
                }

                // 4. Preparar la respuesta HTTP
                if (resultData != null)
                {
                    _logger.LogInformation($"✅ Trámite encontrado. Estado: {resultData.EstadoTramite}");
                    var okResponse = req.CreateResponse(HttpStatusCode.OK);
                    okResponse.Headers.Add("Content-Type", "application/json");
                    await okResponse.WriteStringAsync(JsonConvert.SerializeObject(resultData));
                    return okResponse;
                }
                else
                {
                    _logger.LogWarning($"⚠️ No se encontró trámite para NIT: {nit}, Trámite: {numeroTramite}");
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync(JsonConvert.SerializeObject(new { Mensaje = "Trámite no encontrado o credenciales inválidas." }));
                    return notFoundResponse;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al consultar el estado del trámite en la base de datos.");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonConvert.SerializeObject(new { Mensaje = "Error interno del servidor al consultar el estado del trámite.", Detalle = ex.Message }));
            return errorResponse;
        }
    }
}