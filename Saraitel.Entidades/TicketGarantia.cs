using System;

namespace Saraitel.Entidades
{
    // HU-22 y HU-23: ticket de garantía por un producto defectuoso
    public class TicketGarantia
    {
        // Plazo de la garantía desde la entrega
        public const int MesesDeGarantia = 12;

        // Evidencias (fotos opcionales)
        public const int MaximoEvidencias = 3;
        public const long TamanoMaximoEvidencia = 5 * 1024 * 1024;

        // Fases: Recibido → En revisión → Aprobado / Rechazado → Producto recibido → Cambio enviado
        public const string EstadoRecibido = "Recibido";
        public const string EstadoEnRevision = "En revisión";
        public const string EstadoAprobado = "Aprobado";
        public const string EstadoRechazado = "Rechazado";
        public const string EstadoProductoRecibido = "Producto recibido";
        public const string EstadoCambioEnviado = "Cambio enviado";

        // Quién cubre el daño (dato informativo)
        public const string CubreTienda = "Tienda";
        public const string CubreFabricante = "Fabricante";

        public int IdTicket { get; set; }

        // GT-00001, GT-00002...
        public string Numero { get; set; } = string.Empty;

        public int IdPedido { get; set; }

        public int IdUsuario { get; set; }

        public string NombreCliente { get; set; } = string.Empty;

        public string DocumentoCliente { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        public string Estado { get; set; } = EstadoRecibido;

        // Producto reportado y cuántas unidades
        public LineaPosventa Linea { get; set; }

        public string DescripcionFalla { get; set; } = string.Empty;

        // Nombres de las fotos (GT-00001-1.jpg); el contenido está en la tabla garantia_evidencias
        public List<string> Evidencias { get; set; }

        public string CubiertoPor { get; set; } = string.Empty;

        public string MotivoRechazo { get; set; } = string.Empty;

        // Pedido por $0 que se crea para enviar el producto nuevo
        public int? IdPedidoReposicion { get; set; }

        public string ProductoReposicion { get; set; } = string.Empty;

        public List<HistorialEstado> Historial { get; set; }

        // Un ticket rechazado libera sus unidades para otra solicitud
        public bool OcupaUnidades()
        {
            return Estado != EstadoRechazado;
        }

        public bool EstaEnCurso()
        {
            return Estado != EstadoRechazado && Estado != EstadoCambioEnviado;
        }

        public TicketGarantia()
        {
            Linea = new LineaPosventa();
            Evidencias = new List<string>();
            Historial = new List<HistorialEstado>();
        }
    }
}
