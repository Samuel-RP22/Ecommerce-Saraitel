using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class PedidoService : IPedidoService
    {
        private readonly PedidoRepository _pedidoRepo;
        private readonly UsuarioRepository _usuarioRepo;

        public PedidoService()
        {
            _pedidoRepo = new PedidoRepository();
            _usuarioRepo = new UsuarioRepository();
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

            usuario.HistorialCompras.Add(pedido);

            usuario.Carrito.ListaProductos.Clear();
            usuario.Carrito.CantidadProductos = 0;
            usuario.Carrito.TotalCompra = 0;

            _usuarioRepo.GuardarCambios(usuarios);

            return $"Compra realizada correctamente. Pedido #{pedido.IdPedido}";
        }
    }
}