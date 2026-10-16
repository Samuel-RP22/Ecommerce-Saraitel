using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saraitel.Entidades
{
    public class CarritoCompra
    {
        public int IdCarrito { get; set; }

        public List<Producto> ListaProductos { get; set; }

        public int CantidadProductos { get; set; }

        public decimal TotalCompra { get; set; }

        public CarritoCompra()
        {
            ListaProductos = new List<Producto>();
        }
    }
}