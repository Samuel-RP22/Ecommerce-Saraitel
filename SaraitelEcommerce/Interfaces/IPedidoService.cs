using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface IPedidoService
    {
        List<Pedido> ObtenerPedidos();
        Pedido? ObtenerPorId(int id);
        string AgregarPedido(Pedido pedido);
        string CrearPedidoDesdeCarrito(int idUsuario);
    }
}