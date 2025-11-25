using System.Collections.Generic;

namespace InscripcionEntidades.DTOs
{
    // Estructura de la respuesta que el frontend espera
    public class FiltrosRespuesta
    {
        public List<Filtro> Sectores { get; set; } = new List<Filtro>();
        public List<Filtro> Estados { get; set; } = new List<Filtro>();
    }
}