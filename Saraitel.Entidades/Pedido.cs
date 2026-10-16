using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saraitel.Entidades
{
    public class Pedido
    {
        public int IdPedido { get; set; }

        public DateTime Fecha { get; set; }

        public string EstadoPedido { get; set; } = string.Empty;

        public decimal TotalPedido { get; set; }

        public List<Producto> Productos { get; set; }

        public Pedido()
        {
            Fecha = DateTime.Now;
            EstadoPedido = "Pendiente";
            Productos = new List<Producto>();
        }
    }
}
