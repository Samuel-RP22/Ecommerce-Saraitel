using Saraitel.API.Models;
using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface IPedidoService
    {
        List<Pedido> ObtenerPedidos();
        Pedido? ObtenerPorId(int id);

        List<Pedido> ObtenerPedidosDeUsuario(int idUsuario);
        string AgregarPedido(Pedido pedido);
        string CrearPedidoDesdeCarrito(int idUsuario);

        (Pedido? Pedido, string Mensaje) CrearPedidoCheckout(int idUsuario, SolicitudCheckout solicitud);

        (DatosPagoWompi? Datos, string Mensaje) IniciarPago(int idPedido, int idUsuario);

        Task<ResultadoPago> ConfirmarPagoAsync(int idPedido, int idUsuario, string idTransaccion);

        string CancelarPedido(int idPedido, int idUsuario);

        int CancelarPedidosVencidos();

        (byte[]? Pdf, string NombreArchivo, string Mensaje) ObtenerFacturaPdf(int idPedido, int idUsuario);

        string MarcarEnviado(int idPedido, int idAdministrador, string transportadora, string numeroGuia);

        string MarcarEntregado(int idPedido, int idAdministrador);

        string CancelarPedidoAdmin(int idPedido, int idAdministrador);
    }
}