namespace Saraitel.API.Models
{
    // Una fila del listado de "Consultar compras"
    public class ResumenCompra
    {
        public int IdPedido { get; set; }

        public string NumeroFactura { get; set; } = string.Empty;

        public DateTime FechaCompra { get; set; }

        // Solo para el administrador (vacío para el cliente)
        public string Cliente { get; set; } = string.Empty;

        public string TipoDocumento { get; set; } = string.Empty;

        public string NumeroDocumento { get; set; } = string.Empty;

        public decimal TotalPedido { get; set; }

        public string MedioPago { get; set; } = string.Empty;

        public string EstadoPedido { get; set; } = string.Empty;
    }
}
