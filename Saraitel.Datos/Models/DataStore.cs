using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Saraitel.Entidades;

namespace Saraitel.Datos.Models
{
    public class DataStore
    {
        public List<Usuario> Usuarios { get; set; }
        public List<Producto> Productos { get; set; }
        public List<Categoria> Categorias { get; set; }
        public List<Pedido> Pedidos { get; set; }
        public List<Soporte> Soportes { get; set; }

        public DataStore()
        {
            Usuarios = new List<Usuario>();
            Productos = new List<Producto>();
            Categorias = new List<Categoria>();
            Pedidos = new List<Pedido>();
            Soportes = new List<Soporte>();
        }
    }
}