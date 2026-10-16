using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class CarritoService : ICarritoService
    {
        private readonly UsuarioRepository _usuarioRepo;
        private readonly ProductoRepository _productoRepo;

        public CarritoService()
        {
            _usuarioRepo = new UsuarioRepository();
            _productoRepo = new ProductoRepository();
        }

        public CarritoCompra ObtenerCarrito(int idUsuario)
        {
            var usuarios = _usuarioRepo.ObtenerUsuarios();

            var usuario = usuarios
                .FirstOrDefault(u => u.IdUsuario == idUsuario)
                as Cliente;

            if (usuario == null)
                return null;

            return usuario.Carrito;
        }

        public string AgregarProducto(
            int idUsuario,
            int idProducto,
            int cantidad)
        {
            var usuarios = _usuarioRepo.ObtenerUsuarios();

            var usuario = usuarios
                .FirstOrDefault(u => u.IdUsuario == idUsuario)
                as Cliente;

            if (usuario == null)
                return "Usuario no encontrado";

            var producto = _productoRepo
                .ObtenerProductoPorId(idProducto);

            if (producto == null)
                return "Producto no encontrado";

            for (int i = 0; i < cantidad; i++)
            {
                usuario.Carrito.ListaProductos.Add(producto);
            }

            usuario.Carrito.CantidadProductos =
                usuario.Carrito.ListaProductos.Count;

            usuario.Carrito.TotalCompra =
                usuario.Carrito.ListaProductos.Sum(p => p.Precio);

            _usuarioRepo.GuardarCambios(usuarios);

            return "Producto agregado al carrito";
        }

        public string EliminarProducto(
            int idUsuario,
            int idProducto)
        {
            var usuarios = _usuarioRepo.ObtenerUsuarios();

            var usuario = usuarios
                .FirstOrDefault(u => u.IdUsuario == idUsuario)
                as Cliente;

            if (usuario == null)
                return "Usuario no encontrado";

            var producto = usuario.Carrito
                .ListaProductos
                .FirstOrDefault(p => p.IdProducto == idProducto);

            if (producto == null)
                return "Producto no está en el carrito";

            usuario.Carrito.ListaProductos.Remove(producto);

            usuario.Carrito.CantidadProductos =
                usuario.Carrito.ListaProductos.Count;

            usuario.Carrito.TotalCompra =
                usuario.Carrito.ListaProductos.Sum(p => p.Precio);

            _usuarioRepo.GuardarCambios(usuarios);

            return "Producto eliminado del carrito";
        }

        public string VaciarCarrito(int idUsuario)
        {
            var usuarios = _usuarioRepo.ObtenerUsuarios();

            var usuario = usuarios
                .FirstOrDefault(u => u.IdUsuario == idUsuario)
                as Cliente;

            if (usuario == null)
                return "Usuario no encontrado";

            usuario.Carrito.ListaProductos.Clear();
            usuario.Carrito.CantidadProductos = 0;
            usuario.Carrito.TotalCompra = 0;

            _usuarioRepo.GuardarCambios(usuarios);

            return "Carrito vaciado";
        }

        public decimal CalcularTotal(int idUsuario)
        {
            var usuarios = _usuarioRepo.ObtenerUsuarios();

            var usuario = usuarios
                .FirstOrDefault(u => u.IdUsuario == idUsuario)
                as Cliente;

            if (usuario == null)
                return 0;

            return usuario.Carrito
                .ListaProductos
                .Sum(p => p.Precio);
        }

    }
}