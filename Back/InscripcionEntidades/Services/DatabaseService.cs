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
                    COALESCE(he.TN05_TM01_EstadoActual, 0),
                    COALESCE(est.TM01_Nombre, 'Sin estado')
                FROM TM02_ENTIDADFINANCIERA ef
                LEFT JOIN TM01_SECTORFINANCIERO sf ON ef.TM02_TM01_CodigoSectorF = sf.TM01_CODIGO
                LEFT JOIN (
                    SELECT TN05_TM02_Codigo, TN05_TM01_EstadoActual,
                           ROW_NUMBER() OVER (PARTITION BY TN05_TM02_Codigo ORDER BY TN05_Fecha DESC) as rn
                    FROM TN05_Historico_Estado
                ) he ON ef.TM02_CODIGO = he.TN05_TM02_Codigo AND he.rn = 1
                LEFT JOIN TM01_Estado est ON he.TN05_TM01_EstadoActual = est.TM01_Codigo
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
            
            var query = @"
                SELECT 
                    ef.TM02_CODIGO as Id,
                    ef.TM02_NOMBRE as RazonSocial,
                    ef.TM02_NIT as Nit,
                    ef.TM02_TM01_CodigoSectorF as SectorId,
                    he.TN05_TM01_EstadoActual as EstadoId,
                    est.TM01_Nombre as EstadoNombre
                FROM TM02_ENTIDADFINANCIERA ef
                LEFT JOIN (
                    SELECT TN05_TM02_Codigo, TN05_TM01_EstadoActual,
                           ROW_NUMBER() OVER (PARTITION BY TN05_TM02_Codigo ORDER BY TN05_Fecha DESC) as rn
                    FROM TN05_Historico_Estado
                ) he ON ef.TM02_CODIGO = he.TN05_TM02_Codigo AND he.rn = 1
                LEFT JOIN TM01_Estado est ON he.TN05_TM01_EstadoActual = est.TM01_Codigo";

            var parameters = new List<SqlParameter>();

            if (sectorId.HasValue)
            {
                query += " AND ef.TM02_TM01_CODIGO = @SectorId";
                parameters.Add(new SqlParameter("@SectorId", sectorId.Value));
            }

            if (estadoIds != null && estadoIds.Any())
            {
                var estadoParams = new List<string>();
                for (int i = 0; i < estadoIds.Count; i++)
                {
                    var paramName = $"@EstadoId{i}";
                    estadoParams.Add(paramName);
                    parameters.Add(new SqlParameter(paramName, estadoIds[i]));
                }
                query += $" AND he.TN05_TM01_EstadoActual IN ({string.Join(",", estadoParams)})";
            }
            else if (estadoId.HasValue)
            {
                query += " AND he.TN05_TM01_EstadoActual = @EstadoId";
                parameters.Add(new SqlParameter("@EstadoId", estadoId.Value));
            }
            else
            {
                query += " AND he.TN05_TM01_EstadoActual IN (12, 13, 14)";
            }

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);
            
            command.CommandTimeout = 30; // 30 segundos timeout
            command.Parameters.AddRange(parameters.ToArray());
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                entidades.Add(new EntidadFiltro
                {
                    Id = reader.GetInt32(0),
                    RazonSocial = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Nit = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SectorId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    EstadoId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    EstadoNombre = reader.IsDBNull(5) ? "Sin estado" : reader.GetString(5)
                });
            }

            return entidades;
        }
    }
}