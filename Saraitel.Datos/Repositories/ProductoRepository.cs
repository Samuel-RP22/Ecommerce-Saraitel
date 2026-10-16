using System.Collections.Generic;
using System.Linq;
using Saraitel.Datos.Managers;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class ProductoRepository
    {
        private readonly JsonManager _jsonManager;

        public ProductoRepository()
        {
            _jsonManager = JsonManager.Instance;
        }

        public List<Producto> ObtenerProductos()
        {
            return _jsonManager.LeerDatos().Productos;
        }

        public void AgregarProducto(Producto producto)
        {
            var data = _jsonManager.LeerDatos();
            if (data.Productos == null)
            {
                data.Productos = new List<Producto>();
            }
            int nuevoId = data.Productos.Any() ? data.Productos.Max(p => p.IdProducto) + 1 : 1;
            producto.IdProducto = nuevoId;
            data.Productos.Add(producto);
            _jsonManager.GuardarDatos(data);
        }

        public void EliminarProducto(int id)
        {
            var data = _jsonManager.LeerDatos();
            var producto = data.Productos.FirstOrDefault(p => p.IdProducto == id);

            if (producto != null)
            {
                data.Productos.Remove(producto);
                _jsonManager.GuardarDatos(data);
            }
        }

        public void ActualizarProducto(Producto productoActualizado)
        {
            var data = _jsonManager.LeerDatos();
            var producto = data.Productos.FirstOrDefault(p => p.IdProducto == productoActualizado.IdProducto);

            if (producto != null)
            {
                producto.Nombre = productoActualizado.Nombre;
                producto.Precio = productoActualizado.Precio;
                producto.Stock = productoActualizado.Stock;
                producto.Descripcion = productoActualizado.Descripcion;
                producto.Imagen = productoActualizado.Imagen;
                producto.Categoria = productoActualizado.Categoria;

                _jsonManager.GuardarDatos(data);
            }
        }

        public Producto? ObtenerProductoPorId(int id)
        {
            return ObtenerProductos().FirstOrDefault(p => p.IdProducto == id);
        }
    }
}