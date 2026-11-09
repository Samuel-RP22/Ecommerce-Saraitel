using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class ProductoService : IProductoService
    {
        private readonly ProductoRepository _repo;
        private readonly CategoriaRepository _categoriaRepo;

        // Límites de los campos (la base de datos no acepta más que esto)
        private const int LargoMaximoNombre = 150;
        private const int LargoMaximoDescripcion = 1000;
        private const int LargoMaximoImagen = 1000;
        private const decimal PrecioMaximo = 1_000_000_000m;

        public ProductoService(ProductoRepository repo, CategoriaRepository categoriaRepo)
        {
            _repo = repo;
            _categoriaRepo = categoriaRepo;
        }

        public List<Producto> ObtenerProductos() => _repo.ObtenerProductos();

        public Producto? ObtenerPorId(int id) => _repo.ObtenerProductoPorId(id);

        public string AgregarProducto(Producto producto)
        {
            if (producto == null)
                return "No llegaron los datos del producto";

            string? error = Validar(producto);

            if (error != null)
                return error;

            AsignarCategoria(producto);

            try
            {
                _repo.AgregarProducto(producto);
            }
            catch (DatosInvalidosException ex)
            {
                // La base de datos rechazó algo que se escapó de la validación:
                // se responde con un mensaje en vez de dejar caer la API
                return ex.Message;
            }

            return "Producto agregado correctamente";
        }

        public string EliminarProducto(int id)
        {
            if (_repo.ObtenerProductoPorId(id) == null)
                return "Producto no encontrado";

            _repo.EliminarProducto(id);
            return "Producto eliminado correctamente";
        }

        public string ActualizarProducto(Producto producto)
        {
            if (producto == null)
                return "No llegaron los datos del producto";

            if (_repo.ObtenerProductoPorId(producto.IdProducto) == null)
                return "Producto no encontrado";

            string? error = Validar(producto);

            if (error != null)
                return error;

            AsignarCategoria(producto);

            try
            {
                _repo.ActualizarProducto(producto);
            }
            catch (DatosInvalidosException ex)
            {
                return ex.Message;
            }

            return "Producto actualizado correctamente";
        }

        /* ---------- Validación ---------- */

        // Devuelve el primer problema encontrado, o null si todo está bien.
        // Además deja los textos limpios (sin espacios al inicio ni al final)
        private static string? Validar(Producto producto)
        {
            producto.Nombre = (producto.Nombre ?? "").Trim();
            producto.Descripcion = (producto.Descripcion ?? "").Trim();
            producto.Imagen = (producto.Imagen ?? "").Trim();

            if (producto.Nombre == "")
                return "El nombre del producto es obligatorio";

            if (producto.Nombre.Length > LargoMaximoNombre)
                return $"El nombre del producto no puede tener más de {LargoMaximoNombre} caracteres";

            if (producto.Descripcion.Length > LargoMaximoDescripcion)
                return $"La descripción no puede tener más de {LargoMaximoDescripcion} caracteres";

            if (producto.Precio <= 0)
                return "El precio debe ser un número mayor que cero";

            if (producto.Precio >= PrecioMaximo)
                return "El precio es demasiado alto";

            // Pesos colombianos: como mucho dos decimales
            if (decimal.Round(producto.Precio, 2) != producto.Precio)
                return "El precio no puede tener más de dos decimales";

            if (producto.Stock < 0)
                return "El stock debe ser un número entero mayor o igual a cero";

            if (producto.Imagen.Length > LargoMaximoImagen)
                return "El enlace de la imagen es demasiado largo";

            // La imagen es opcional, pero si se escribe debe ser un enlace web
            if (producto.Imagen != "" &&
                !(Uri.TryCreate(producto.Imagen, UriKind.Absolute, out var uri) &&
                  (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
                return "El enlace de la imagen debe empezar por http:// o https://";

            return null;
        }

        // Busca la categoría por nombre; si no existe, el producto queda sin categoría
        private void AsignarCategoria(Producto producto)
        {
            string nombreCategoria = (producto.Categoria?.NombreCategoria ?? "").Trim().ToLower();

            var categoriaReal = nombreCategoria == ""
                ? null
                : _categoriaRepo.ObtenerCategorias().FirstOrDefault(c =>
                    (c.NombreCategoria ?? "").Trim().ToLower() == nombreCategoria);

            producto.Categoria = categoriaReal == null
                ? new Categoria()
                : new Categoria
                {
                    IdCategoria = categoriaReal.IdCategoria,
                    NombreCategoria = categoriaReal.NombreCategoria
                };
        }
    }
}
