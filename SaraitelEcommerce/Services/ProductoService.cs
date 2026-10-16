using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class ProductoService : IProductoService
    {
        private readonly ProductoRepository _repo;
        private readonly CategoriaRepository _categoriaRepo;

        public ProductoService(ProductoRepository repo, CategoriaRepository categoriaRepo)
        {
            _repo = repo;
            _categoriaRepo = categoriaRepo;
        }

        public List<Producto> ObtenerProductos() => _repo.ObtenerProductos();

        public Producto? ObtenerPorId(int id) => _repo.ObtenerProductoPorId(id);

        public string AgregarProducto(Producto producto)
        {
            var categoriaRepo = new CategoriaRepository();
            var categoriasExistentes = categoriaRepo.ObtenerCategorias();
            var categoriaReal = categoriasExistentes.FirstOrDefault(c =>
                c.NombreCategoria.Trim().ToLower() == producto.Categoria.NombreCategoria.Trim().ToLower());
            if (categoriaReal != null)
            {
                producto.Categoria.IdCategoria = categoriaReal.IdCategoria;
                producto.Categoria.NombreCategoria = categoriaReal.NombreCategoria;
            }
            else
            {
                producto.Categoria = null;
            }
            _repo.AgregarProducto(producto);

            return "Producto agregado correctamente";
        }

        public string EliminarProducto(int id)
        {
            _repo.EliminarProducto(id);
            return "Producto eliminado correctamente";
        }

        public string ActualizarProducto(Producto producto)
        {
            _repo.ActualizarProducto(producto);
            return "Producto actualizado correctamente";
        }
    }
}