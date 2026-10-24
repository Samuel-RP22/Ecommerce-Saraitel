using System;

namespace Saraitel.Entidades
{
    public class Pedido
    {
        // HU-15: el envío estándar siempre cuesta lo mismo
        public const decimal CostoEnvioEstandar = 15000m;

        // HU-15: los precios de los productos ya incluyen IVA
        public const decimal TasaIva = 0.19m;

        // HU-16: tiempo que tiene el cliente para pagar antes de que se
        // cancele el pedido y el stock vuelva al catálogo
        public const int MinutosParaPagar = 20;

        // Estados que puede tener un pedido
        public const string EstadoPendientePago = "Pendiente de pago";
        public const string EstadoPagado = "Pagado";
        public const string EstadoCancelado = "Cancelado";

        // Envío manual: el administrador los marca en orden Pagado → Enviado → Entregado
        public const string EstadoEnviado = "Enviado";
        public const string EstadoEntregado = "Entregado";

        public int IdPedido { get; set; }

        public int IdUsuario { get; set; }

        public DateTime Fecha { get; set; }

        public string EstadoPedido { get; set; } = string.Empty;

        // Productos copiados uno por unidad. Solo lo usan los pedidos
        // anteriores a la HU-15; los nuevos usan Detalles.
        public List<Producto> Productos { get; set; }

        // HU-15: una línea por producto con precio y cantidad congelados
        public List<DetallePedido> Detalles { get; set; }

        public DatosEnvio? Envio { get; set; }

        public DatosEnvio? Facturacion { get; set; }

        public string MetodoEnvio { get; set; } = string.Empty;

        public string MetodoPago { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }

        public decimal CostoEnvio { get; set; }

        // IVA que ya viene incluido en el subtotal de los productos
        public decimal Impuestos { get; set; }

        public decimal TotalPedido { get; set; }

        // HU-16: datos del pago con Wompi
        public DateTime? FechaLimitePago { get; set; }

        // Referencia del último intento de pago (cambia en cada reintento)
        public string ReferenciaPago { get; set; } = string.Empty;

        public string IdTransaccion { get; set; } = string.Empty;

        // Medio con el que se pagó: Tarjeta, PSE, Nequi, etc.
        public string MedioPago { get; set; } = string.Empty;

        public DateTime? FechaPago { get; set; }

        public string MotivoCancelacion { get; set; } = string.Empty;

        // HU-17: factura del pedido. Solo existe cuando el pago fue aprobado.
        // El número es consecutivo y sin huecos: FV-00001, FV-00002...
        public string NumeroFactura { get; set; } = string.Empty;

        public DateTime? FechaFactura { get; set; }

        // Datos del envío que registra el administrador
        public string Transportadora { get; set; } = string.Empty;

        public string NumeroGuia { get; set; } = string.Empty;

        public DateTime? FechaEnvio { get; set; }

        public DateTime? FechaEntrega { get; set; }

        // HU-23: un pedido puede ser una venta o la reposición de un producto
        // en garantía (se envía sin costo y no tiene factura)
        public const string TipoVenta = "Venta";
        public const string TipoReposicion = "Reposición";

        public string TipoPedido { get; set; } = TipoVenta;

        public int? IdPedidoOriginal { get; set; }

        public string NumeroGarantia { get; set; } = string.Empty;

        public bool EsReposicion()
        {
            return TipoPedido == TipoReposicion;
        }

        // Un pedido enviado o entregado también está pagado
        public bool EstaPagado()
        {
            return EstadoPedido == EstadoPagado
                || EstadoPedido == EstadoEnviado
                || EstadoPedido == EstadoEntregado;
        }

        public Pedido()
        {
            Fecha = DateTime.Now;
            EstadoPedido = "Pendiente";
            Productos = new List<Producto>();
            Detalles = new List<DetallePedido>();
        }
    }
}
