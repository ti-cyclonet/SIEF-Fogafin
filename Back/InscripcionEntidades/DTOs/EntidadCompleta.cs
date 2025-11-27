namespace InscripcionEntidades.DTOs
{
    public class EntidadCompleta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public int SectorId { get; set; }
        public string SectorNombre { get; set; } = string.Empty;
        public int EntidadId { get; set; }
        public DateTime? FechaInscripcion { get; set; }
        public string NombreRepresentante { get; set; } = string.Empty;
        public string CorreoNotificacion { get; set; } = string.Empty;
        public decimal? CapitalSuscrito { get; set; }
        public decimal? ValorPagado { get; set; }
        public DateTime? FechaPago { get; set; }
        public int EstadoId { get; set; }
        public string EstadoNombre { get; set; } = string.Empty;
    }
}