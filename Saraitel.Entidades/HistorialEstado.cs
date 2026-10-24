using System;

namespace Saraitel.Entidades
{
    // Cada cambio de estado de una devolución o garantía, para su seguimiento
    public class HistorialEstado
    {
        public string Estado { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        public string Nota { get; set; } = string.Empty;
    }
}
