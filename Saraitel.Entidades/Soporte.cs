using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saraitel.Entidades
{
    public class Soporte
    {
        public int IdSoporte { get; set; }

        public string Mensaje { get; set; } = string.Empty;

        public DateTime FechaSolicitud { get; set; }

        public string EstadoSolicitud { get; set; } = string.Empty;

        public Soporte()
        {
            FechaSolicitud = DateTime.Now;
            EstadoSolicitud = "Pendiente";
        }
    }
}
