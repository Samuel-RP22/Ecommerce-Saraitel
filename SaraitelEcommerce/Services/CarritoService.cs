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

            if (cantidad <= 0)
                return "La cantidad debe ser mayor a 0";

            // Se valida el stock ANTES de agregar al carrito
            int enCarrito = usuario.Carrito.ListaProductos
                .Count(p => p.IdProducto == idProducto);

            if (enCarrito + cantidad > producto.Stock)
            {
                int maximoAgregable = Math.Max(0, producto.Stock - enCarrito);

                return $"No se puede agregar: excediste el stock disponible de \"{producto.Nombre}\". " +
                       $"Stock disponible: {producto.Stock}. " +
                       $"Ya tienes {enCarrito} en tu carrito, puedes agregar máximo {maximoAgregable} más.";
            }

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

        public string ActualizarCantidad(
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

            if (cantidad < 0)
                return "La cantidad no puede ser negativa";

            var producto = _productoRepo
                .ObtenerProductoPorId(idProducto);

            if (producto == null)
                return "Producto no encontrado";

            // Se valida contra el stock real del catálogo
            if (cantidad > producto.Stock)
            {
                return $"No se puede: excediste el stock disponible de \"{producto.Nombre}\". " +
                       $"Stock disponible: {producto.Stock}.";
            }

            // El carrito guarda una entrada por unidad: se quitan las
            // unidades actuales y se ponen las nuevas en la misma posición,
            // para que el producto no cambie de lugar en el carrito
            var lista = usuario.Carrito.ListaProductos;

            int posicion = lista.FindIndex(p => p.IdProducto == idProducto);

            lista.RemoveAll(p => p.IdProducto == idProducto);

            if (posicion < 0)
                posicion = lista.Count;

            lista.InsertRange(posicion, Enumerable.Repeat(producto, cantidad));

            usuario.Carrito.CantidadProductos =
                usuario.Carrito.ListaProductos.Count;

            usuario.Carrito.TotalCompra =
                usuario.Carrito.ListaProductos.Sum(p => p.Precio);

            _usuarioRepo.GuardarCambios(usuarios);

            return "Cantidad actualizada";
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