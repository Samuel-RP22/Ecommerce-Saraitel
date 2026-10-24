using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface ICarritoService
    {
        CarritoCompra ObtenerCarrito(int idUsuario);

        string AgregarProducto(int idUsuario, int idProducto, int cantidad);

        string EliminarProducto(int idUsuario, int idProducto);

        string ActualizarCantidad(int idUsuario, int idProducto, int cantidad);

        string VaciarCarrito(int idUsuario);

        decimal CalcularTotal(int idUsuario);
    }
}