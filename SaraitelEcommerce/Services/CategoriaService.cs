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
            // La base de datos deja sin categoría a sus productos (ON DELETE SET NULL)
            _repo.EliminarCategoria(id);
            return "Categoria eliminada";
        }
    }
}