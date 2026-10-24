using System;

namespace Saraitel.Entidades
{
    // Una línea del pedido. Los datos se copian al momento de la compra,
    // así un cambio de precio o de nombre del producto no altera pedidos pasados.
    public class DetallePedido
    {
        public int IdProducto { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Imagen { get; set; } = string.Empty;

        public decimal PrecioUnitario { get; set; }

        public int Cantidad { get; set; }

        public decimal Subtotal { get; set; }
    }
}
