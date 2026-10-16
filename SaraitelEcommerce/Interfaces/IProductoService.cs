using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface IProductoService
    {
        List<Producto> ObtenerProductos();
        Producto? ObtenerPorId(int id);
        string AgregarProducto(Producto producto);
        string EliminarProducto(int id);
        string ActualizarProducto(Producto producto);
    }
}