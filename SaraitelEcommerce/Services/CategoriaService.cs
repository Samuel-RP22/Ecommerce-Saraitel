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
            int nuevoId = ObtenerCategorias().Any()? ObtenerCategorias().Max(c => c.IdCategoria) + 1: 1;
            categoria.IdCategoria = nuevoId;
            _repo.AgregarCategoria(categoria);
            return categoria;
        }

        public string EliminarCategoria(int id)
        {
            var productos = _productoRepo.ObtenerProductos();
            foreach (var producto in productos)
            {
                if (producto.Categoria != null &&
                    producto.Categoria.IdCategoria == id)
                {
                    producto.Categoria = new Categoria
                    {
                        IdCategoria = 0,
                        NombreCategoria = ""
                    };
                    _productoRepo.ActualizarProducto(producto);
                }
            }
            _repo.EliminarCategoria(id);
            return "Categoria eliminada";
        }
    }
}