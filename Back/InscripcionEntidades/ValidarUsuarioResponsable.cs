using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace InscripcionEntidades
{
    public class ValidarUsuarioResponsable
    {
        private readonly ILogger _logger;

        public ValidarUsuarioResponsable(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ValidarUsuarioResponsable>();
        }

        [Function("ValidarUsuarioResponsable")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "validar-usuario")] HttpRequestData req)
        {
            _logger.LogInformation("Validando usuario en tabla TM04_Responsables");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
                
                string usuario = data["usuario"].ToString();

                _logger.LogInformation($"Validando usuario: '{usuario}'");

                // 1. Simulación de validación en Directorio Activo (solo para AdminSief)
                if (usuario.Equals("adminSief", StringComparison.OrdinalIgnoreCase))
                {
                    bool existeEnDirectorioActivo = SimularDirectorioActivo(usuario);
                    _logger.LogInformation($"Usuario '{usuario}' en Directorio Activo: {existeEnDirectorioActivo}");
                    
                    if (!existeEnDirectorioActivo)
                    {
                        _logger.LogInformation($"Usuario '{usuario}' no encontrado en Directorio Activo");
                        var response = req.CreateResponse(HttpStatusCode.OK);
                        response.Headers.Add("Content-Type", "application/json");
                        var resultado = new { esValido = false, motivo = "Usuario no encontrado en Directorio Activo" };
                        await response.WriteStringAsync(JsonSerializer.Serialize(resultado));
                        return response;
                    }
                }

                // 2. Bypass para administrador
                if (usuario.Equals("adminSief", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Usuario administrador '{usuario}' - acceso total concedido");
                    var adminResponse = req.CreateResponse(HttpStatusCode.OK);
                    adminResponse.Headers.Add("Content-Type", "application/json");
                    var adminResultado = new { 
                        esValido = true, 
                        codigoArea = "ADMIN",
                        nombreArea = "Administrador SIEF"
                    };
                    await adminResponse.WriteStringAsync(JsonSerializer.Serialize(adminResultado));
                    return adminResponse;
                }

                // 3. Si existe en AD, validar en base de datos
                string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT r.TM04_TM03_Codigo, ISNULL(a.TM02_Nombre, 'SIEF') as TM02_Nombre, c.TM15_TM14_Perfil
                        FROM [SistemasComunes].[dbo].[TM04_Responsables] r
                        LEFT JOIN [SIIR-ProdV1].[dbo].[TM02_Area] a ON r.TM04_TM03_Codigo = a.TM02_Codigo
                        INNER JOIN [SistemasComunes].[dbo].[TM15_ConexionAppAmbXResponsable] c ON r.TM04_Identificacion = c.TM15_TM04_Identificacion
                        WHERE r.TM04_Identificacion = @usuario 
                        AND r.TM04_Activo = 1
                        AND c.TM15_TM12_TM01_Codigo = 17
                        AND c.TM15_TM12_Ambiente IN ('PROD', 'PRODUCCION', 'PRUEBAS')";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@usuario", usuario);
                        
                        _logger.LogInformation($"Ejecutando consulta para ambientes PROD y PRODUCCION con usuario: {usuario}");

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var codigoArea = reader["TM04_TM03_Codigo"].ToString();
                                var nombreArea = reader["TM02_Nombre"].ToString();
                                var perfil = reader["TM15_TM14_Perfil"].ToString();
                                
                                _logger.LogInformation($"Usuario '{usuario}' válido y autorizado, área: {nombreArea} ({codigoArea}), perfil: {perfil}");

                                var response = req.CreateResponse(HttpStatusCode.OK);
                                response.Headers.Add("Content-Type", "application/json");
                                
                                var resultado = new { 
                                    esValido = true, 
                                    codigoArea = codigoArea,
                                    nombreArea = nombreArea,
                                    perfil = perfil
                                };
                                await response.WriteStringAsync(JsonSerializer.Serialize(resultado));

                                return response;
                            }
                            else
                            {
                                _logger.LogInformation($"Usuario '{usuario}' no encontrado o no autorizado para acceder al sistema SIEF");

                                var response = req.CreateResponse(HttpStatusCode.OK);
                                response.Headers.Add("Content-Type", "application/json");
                                
                                var resultado = new { esValido = false, motivo = "Usuario no autorizado para SIEF" };
                                await response.WriteStringAsync(JsonSerializer.Serialize(resultado));

                                return response;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar usuario");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error interno del servidor");
                return errorResponse;
            }
        }

        // Simulación de validación en Directorio Activo
        private bool SimularDirectorioActivo(string usuario)
        {
            // Simulación: usuarios que existen en AD
            var usuariosValidos = new[] { "adminSief" };
            return usuariosValidos.Contains(usuario, StringComparer.OrdinalIgnoreCase);
        }
    }
}