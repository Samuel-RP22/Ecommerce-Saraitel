using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface ICategoriaService
    {
        List<Categoria> ObtenerCategorias();
        Categoria? ObtenerPorId(int id);
        Categoria? AgregarCategoria(Categoria categoria);
        string EliminarCategoria(int id);
    }
}