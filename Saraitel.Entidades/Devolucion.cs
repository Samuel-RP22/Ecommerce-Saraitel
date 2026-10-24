using System;

namespace Saraitel.Entidades
{
    // HU-19 y HU-21: devolución de productos de un pedido entregado (derecho de retracto)
    public class Devolucion
    {
        // Plazo para pedir la devolución: días hábiles desde la entrega
        public const int DiasHabilesParaDevolver = 5;

        // Estados: Solicitada → Aprobada / Rechazada → Producto recibido → Reembolsada
        public const string EstadoSolicitada = "Solicitada";
        public const string EstadoAprobada = "Aprobada";
        public const string EstadoRechazada = "Rechazada";
        public const string EstadoProductoRecibido = "Producto recibido";
        public const string EstadoReembolsada = "Reembolsada";

        public static readonly string[] Motivos =
        {
            "Ya no lo quiero",
            "No es lo que esperaba",
            "Me equivoqué de producto",
            "Otro"
        };

        public int IdDevolucion { get; set; }

        // DV-00001, DV-00002...
        public string Numero { get; set; } = string.Empty;

        public int IdPedido { get; set; }

        public int IdUsuario { get; set; }

        public string NombreCliente { get; set; } = string.Empty;

        public string DocumentoCliente { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        public string Estado { get; set; } = EstadoSolicitada;

        public List<LineaPosventa> Lineas { get; set; }

        public string Motivo { get; set; } = string.Empty;

        public string Comentario { get; set; } = string.Empty;

        // El envío se reembolsa solo si se devuelve el pedido completo
        public bool IncluyeEnvio { get; set; }

        public decimal ValorProductos { get; set; }

        public decimal ValorEnvio { get; set; }

        public decimal ValorReembolso { get; set; }

        public string MotivoRechazo { get; set; } = string.Empty;

        public string ReferenciaReembolso { get; set; } = string.Empty;

        public DateTime? FechaReembolso { get; set; }

        public List<HistorialEstado> Historial { get; set; }

        // Una devolución rechazada libera sus unidades para otra solicitud
        public bool OcupaUnidades()
        {
            return Estado != EstadoRechazada;
        }

        public bool EstaEnCurso()
        {
            return Estado != EstadoRechazada && Estado != EstadoReembolsada;
        }

        public Devolucion()
        {
            Lineas = new List<LineaPosventa>();
            Historial = new List<HistorialEstado>();
        }
    }
}
