using Saraitel.Entidades;

namespace Saraitel.API.Models
{
    // HU-19: lo que envía el cliente para pedir una devolución
    public class SolicitudDevolucion
    {
        public int IdPedido { get; set; }

        public List<LineaSolicitada> Lineas { get; set; } = new List<LineaSolicitada>();

        public string Motivo { get; set; } = string.Empty;

        public string Comentario { get; set; } = string.Empty;
    }

    public class LineaSolicitada
    {
        public int IdProducto { get; set; }

        public int Cantidad { get; set; }
    }

    // HU-22: lo que envía el cliente para reportar una garantía (formulario con fotos)
    public class SolicitudGarantia
    {
        public int IdUsuario { get; set; }

        public int IdPedido { get; set; }

        public int IdProducto { get; set; }

        public int Cantidad { get; set; }

        public string Descripcion { get; set; } = string.Empty;

        // Fotos opcionales (máximo 3, JPG o PNG de hasta 5 MB)
        public List<IFormFile>? Fotos { get; set; }
    }

    // Una foto ya leída, lista para guardarse
    public class ArchivoEvidencia
    {
        public string NombreOriginal { get; set; } = string.Empty;

        public byte[] Contenido { get; set; } = Array.Empty<byte>();
    }

    // Lo que el cliente puede hacer con un pedido y sus solicitudes
    public class ResumenPosventaPedido
    {
        public bool PuedeDevolver { get; set; }

        public string MensajeDevolucion { get; set; } = string.Empty;

        public DateTime? FechaLimiteDevolucion { get; set; }

        public bool PuedeReportarGarantia { get; set; }

        public string MensajeGarantia { get; set; } = string.Empty;

        public DateTime? FechaLimiteGarantia { get; set; }

        public decimal CostoEnvio { get; set; }

        public int TotalUnidades { get; set; }

        // Unidades que ya están en devoluciones no rechazadas
        public int UnidadesEnDevolucion { get; set; }

        public bool EsReposicion { get; set; }

        public List<LineaDisponible> Lineas { get; set; } = new List<LineaDisponible>();

        public List<Devolucion> Devoluciones { get; set; } = new List<Devolucion>();

        public List<TicketGarantia> Garantias { get; set; } = new List<TicketGarantia>();
    }

    public class LineaDisponible
    {
        public int IdProducto { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Imagen { get; set; } = string.Empty;

        public decimal PrecioUnitario { get; set; }

        public int Comprada { get; set; }

        // Unidades que todavía no tienen devolución ni garantía
        public int Disponible { get; set; }
    }

    // Devoluciones y garantías de un cliente
    public class PosventaUsuario
    {
        public List<Devolucion> Devoluciones { get; set; } = new List<Devolucion>();

        public List<TicketGarantia> Garantias { get; set; } = new List<TicketGarantia>();
    }
}
