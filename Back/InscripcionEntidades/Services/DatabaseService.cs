using System.Data.SqlClient;
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

        public async Task<List<EntidadFiltro>> GetEntidadesFiltradasAsync(int? sectorId = null, int? estadoId = null)
        {
            var entidades = new List<EntidadFiltro>();
            
            var query = @"
                SELECT 
                    ef.TM02_CODIGO as Id,
                    ef.TM02_NOMBRE as RazonSocial,
                    ef.TM02_NIT as Nit,
                    ef.TM02_TM01_CODIGO as SectorId,
                    he.TN05_TM01_EstadoActual as EstadoId,
                    est.TM01_Nombre as EstadoNombre
                FROM TM02_ENTIDADFINANCIERA ef
                INNER JOIN (
                    SELECT TN05_TM02_Codigo, TN05_TM01_EstadoActual,
                           ROW_NUMBER() OVER (PARTITION BY TN05_TM02_Codigo ORDER BY TN05_Fecha DESC) as rn
                    FROM TN05_Historico_Estado
                ) he ON ef.TM02_CODIGO = he.TN05_TM02_Codigo AND he.rn = 1
                INNER JOIN TM01_Estado est ON he.TN05_TM01_EstadoActual = est.TM01_Codigo
                WHERE ef.TM02_ACTIVO = 0";

            var parameters = new List<SqlParameter>();

            if (sectorId.HasValue)
            {
                query += " AND ef.TM02_TM01_CODIGO = @SectorId";
                parameters.Add(new SqlParameter("@SectorId", sectorId.Value));
            }

            if (estadoId.HasValue)
            {
                query += " AND he.TN05_TM01_EstadoActual = @EstadoId";
                parameters.Add(new SqlParameter("@EstadoId", estadoId.Value));
            }
            else
            {
                query += " AND he.TN05_TM01_EstadoActual BETWEEN 1 AND 14";
            }

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);
            
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
                    SectorId = reader.GetInt32(3),
                    EstadoId = reader.GetInt32(4),
                    EstadoNombre = reader.IsDBNull(5) ? "" : reader.GetString(5)
                });
            }

            return entidades;
        }
    }
}