namespace InscripcionEntidades.DTOs
{
    public class EntidadFiltro
    {
        public int Id { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public int SectorId { get; set; }
        public int EstadoId { get; set; }
        public string EstadoNombre { get; set; } = string.Empty;
    }
}