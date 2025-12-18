using Microsoft.Data.SqlClient;
using InscripcionEntidades.DTOs;

namespace InscripcionEntidades.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<EntidadCompleta>> GetTodasLasEntidadesAsync()
        {
            var entidades = new List<EntidadCompleta>();
            
            var query = @"
                SELECT 
                    ef.TM02_CODIGO,
                    ef.TM02_NOMBRE,
                    ef.TM02_NIT,
                    ISNULL(ef.TM02_TM01_CodigoSectorF, 0),
                    ISNULL(sf.TM01_NOMBRE, ''),
                    ef.TM02_CODIGO,
                    ef.TM02_FECHAINSCRIPCION,
                    ISNULL(ef.TM02_Nombre_Rep, ''),
                    ISNULL(ef.TM02_Correo_Noti, ''),
                    ISNULL(ef.TM02_CapitalSuscrito, 0),
                    ISNULL(ef.TM02_ValorPagado, 0),
                    ef.TM02_FechaPago,
                    ef.TM02_TM01_CODIGO,
                    ISNULL(est.TM01_Nombre, 'Sin estado')
                FROM TM02_ENTIDADFINANCIERA ef
                LEFT JOIN TM01_SECTORFINANCIERO sf ON ef.TM02_TM01_CodigoSectorF = sf.TM01_CODIGO
                LEFT JOIN TM01_Estado est ON ef.TM02_TM01_CODIGO = est.TM01_Codigo
                ORDER BY ef.TM02_FECHAINSCRIPCION DESC";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);
            
            command.CommandTimeout = 30;
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                entidades.Add(new EntidadCompleta
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Nit = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SectorId = reader.GetInt32(3),
                    SectorNombre = reader.GetString(4),
                    EntidadId = reader.GetInt32(5),
                    FechaInscripcion = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    NombreRepresentante = reader.GetString(7),
                    CorreoNotificacion = reader.GetString(8),
                    CapitalSuscrito = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                    ValorPagado = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                    FechaPago = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    EstadoId = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    EstadoNombre = reader.IsDBNull(13) ? "Sin estado" : reader.GetString(13)
                });
            }

            return entidades;
        }

        public async Task<List<EntidadFiltro>> GetEntidadesFiltradasAsync(int? sectorId = null, int? estadoId = null, List<int> estadoIds = null)
        {
            var entidades = new List<EntidadFiltro>();
            
            var whereConditions = new List<string>();
            var parameters = new List<SqlParameter>();
            
            // Filtro por sector
            if (sectorId.HasValue)
            {
                whereConditions.Add("ef.TM02_TM01_CodigoSectorF = @SectorId");
                parameters.Add(new SqlParameter("@SectorId", sectorId.Value));
            }
            else
            {
                whereConditions.Add("ef.TM02_TM01_CodigoSectorF IN (1, 2, 4, 28)");
            }
            
            // Filtro por estado(s)
            if (estadoIds != null && estadoIds.Count > 0)
            {
                var estadoParams = string.Join(",", estadoIds.Select((_, i) => $"@EstadoId{i}"));
                whereConditions.Add($"ef.TM02_TM01_CODIGO IN ({estadoParams})");
                for (int i = 0; i < estadoIds.Count; i++)
                {
                    parameters.Add(new SqlParameter($"@EstadoId{i}", estadoIds[i]));
                }
            }
            else if (estadoId.HasValue)
            {
                whereConditions.Add("ef.TM02_TM01_CODIGO = @EstadoId");
                parameters.Add(new SqlParameter("@EstadoId", estadoId.Value));
            }
            
            var whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";
            
            var query = $@"
                SELECT 
                    ef.TM02_CODIGO as Id, 
                    ef.TM02_NOMBRE as RazonSocial, 
                    ef.TM02_NIT as Nit, 
                    ISNULL(ef.TM02_TM01_CodigoSectorF, 0) as SectorId, 
                    ef.TM02_TM01_CODIGO as EstadoId, 
                    ISNULL(est.TM01_Nombre, 'Sin estado') as EstadoNombre,
                    ISNULL(sf.TM01_NOMBRE, '') as TipoEntidad
                FROM TM02_ENTIDADFINANCIERA ef
                LEFT JOIN TM01_SECTORFINANCIERO sf ON ef.TM02_TM01_CodigoSectorF = sf.TM01_CODIGO
                LEFT JOIN TM01_Estado est ON ef.TM02_TM01_CODIGO = est.TM01_Codigo
                {whereClause}
                ORDER BY ef.TM02_FECHAINSCRIPCION DESC";
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;
            command.Parameters.AddRange(parameters.ToArray());
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var entidad = new EntidadFiltro
                {
                    Id = reader.GetInt32(0),
                    RazonSocial = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Nit = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SectorId = reader.GetInt32(3),
                    EstadoId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    EstadoNombre = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    TipoEntidad = reader.IsDBNull(6) ? "" : reader.GetString(6)
                };

                entidades.Add(entidad);
            }

            return entidades;
        }
    }
}