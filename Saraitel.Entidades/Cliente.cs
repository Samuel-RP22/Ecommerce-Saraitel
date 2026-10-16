using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saraitel.Entidades
{
    public class Cliente : Usuario
    {
        public string Direccion { get; set; } = string.Empty;

        public List<Pedido> HistorialCompras { get; set; }

        public CarritoCompra Carrito { get; set; }

        public Cliente()
        {
            HistorialCompras = new List<Pedido>();
            Carrito = new CarritoCompra();
            Rol = "Cliente";
        }
    }
}