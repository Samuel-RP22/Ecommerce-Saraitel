using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class CategoriaService : ICategoriaService
    {
        private readonly CategoriaRepository _repo;
        private readonly ProductoRepository _productoRepo;

        public CategoriaService(
            CategoriaRepository repo,
            ProductoRepository productoRepo)
        {
            _repo = repo;
            _productoRepo = productoRepo;
        }

        public List<Categoria> ObtenerCategorias()
            => _repo.ObtenerCategorias();

        public Categoria? ObtenerPorId(int id)
            => _repo.ObtenerCategoriaPorId(id);

        public Categoria AgregarCategoria(Categoria categoria)
        {
            // Si ya existe una con el mismo nombre, se devuelve esa (no se repiten)
            var existente = ObtenerCategorias().FirstOrDefault(c =>
                c.NombreCategoria.Trim().Equals((categoria.NombreCategoria ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

            if (existente != null)
                return existente;

            // El id lo asigna la base de datos al guardar
            _repo.AgregarCategoria(categoria);
            return categoria;
        }

        public string EliminarCategoria(int id)
        {
            var categoria = _repo.ObtenerCategoriaPorId(id);

            if (categoria == null)
                return "Categoría no encontrada";

            // Una categoría con productos no se puede eliminar:
            // primero hay que pasar esos productos a otra categoría o eliminarlos
            int productos = _repo.ContarProductos(id);

            if (productos > 0)
                return MensajeConProductos(categoria.NombreCategoria, productos);

            if (!_repo.EliminarCategoriaSinProductos(id))
            {
                // Le asignaron un producto mientras se intentaba eliminar
                int ahora = _repo.ContarProductos(id);
                return ahora > 0
                    ? MensajeConProductos(categoria.NombreCategoria, ahora)
                    : "Categoría no encontrada";
            }

            return "Categoría eliminada correctamente";
        }

        private static string MensajeConProductos(string nombre, int cantidad)
        {
            string productos = cantidad == 1 ? "1 producto la usa" : $"{cantidad} productos la usan";

            return $"No se puede eliminar la categoría \"{nombre}\": {productos}. " +
                   "Cambia esos productos a otra categoría o elimínalos primero.";
        }
    }
}