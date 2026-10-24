using System;

namespace Saraitel.Entidades
{
    // Un producto (y cuántas unidades) dentro de una devolución o una garantía.
    // Se copian del pedido, con el precio que se pagó.
    public class LineaPosventa
    {
        public int IdProducto { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Imagen { get; set; } = string.Empty;

        public decimal PrecioUnitario { get; set; }

        public int Cantidad { get; set; }

        public decimal Subtotal { get; set; }
    }
}
