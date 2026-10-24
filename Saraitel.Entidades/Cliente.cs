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

        // Sus compras ya no se copian aquí: están en la tabla pedidos (id_usuario)

        public CarritoCompra Carrito { get; set; }

        public Cliente()
        {
            Carrito = new CarritoCompra();
            Rol = "Cliente";
        }
    }
}