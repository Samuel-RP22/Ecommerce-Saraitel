using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class PedidoService : IPedidoService
    {
        private readonly PedidoRepository _pedidoRepo;
        private readonly UsuarioRepository _usuarioRepo;
        private readonly ProductoRepository _productoRepo;

        public PedidoService()
        {
            _pedidoRepo = new PedidoRepository();
            _usuarioRepo = new UsuarioRepository();
            _productoRepo = new ProductoRepository();
        }

        public List<Pedido> ObtenerPedidos()
        {
            return _pedidoRepo.ObtenerPedidos();
        }

        public Pedido? ObtenerPorId(int id)
        {
            return _pedidoRepo.ObtenerPedidoPorId(id);
        }

        public string AgregarPedido(Pedido pedido)
        {
            _pedidoRepo.AgregarPedido(pedido);
            return "Pedido creado";
        }


        public string CrearPedidoDesdeCarrito(int idUsuario)
        {
            var usuarios = _usuarioRepo.ObtenerUsuarios();

            var usuario = usuarios
                .FirstOrDefault(u => u.IdUsuario == idUsuario) as Cliente;

            if (usuario == null)
                return "Usuario no encontrado";

            if (usuario.Carrito.ListaProductos.Count == 0)
                return "El carrito está vacío";

            // HU-13: unidades pedidas de cada producto (idProducto -> cantidad)
            var cantidades = usuario.Carrito.ListaProductos
                .GroupBy(p => p.IdProducto)
                .ToDictionary(g => g.Key, g => g.Count());

            // Se valida el stock ANTES de crear el pedido
            foreach (var item in cantidades)
            {
                var productoActual = _productoRepo.ObtenerProductoPorId(item.Key);

                if (productoActual == null)
                {
                    string nombre = usuario.Carrito.ListaProductos
                        .First(p => p.IdProducto == item.Key).Nombre;

                    return $"El producto \"{nombre}\" ya no está disponible";
                }

                if (productoActual.Stock < item.Value)
                    return $"Stock insuficiente para \"{productoActual.Nombre}\". " +
                           $"Disponible: {productoActual.Stock}, en tu carrito: {item.Value}";
            }

            Pedido pedido = new Pedido
            {
                IdPedido = ObtenerPedidos().Any()
                    ? ObtenerPedidos().Max(p => p.IdPedido) + 1
                    : 1,

                Productos = usuario.Carrito.ListaProductos.ToList(),

                TotalPedido = usuario.Carrito.ListaProductos.Sum(p => p.Precio),

                EstadoPedido = "Pagado",
                Fecha = DateTime.Now
            };

            _pedidoRepo.AgregarPedido(pedido);

            // HU-13: se descuenta el stock de lo vendido
            _productoRepo.DescontarStock(cantidades);

            usuario.HistorialCompras.Add(pedido);

            usuario.Carrito.ListaProductos.Clear();
            usuario.Carrito.CantidadProductos = 0;
            usuario.Carrito.TotalCompra = 0;

            _usuarioRepo.GuardarCambios(usuarios);

            return $"Compra realizada correctamente. Pedido #{pedido.IdPedido}";
        }
    }
}