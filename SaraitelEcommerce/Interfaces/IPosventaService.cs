using Saraitel.API.Models;
using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface IPosventaService
    {
        (ResumenPosventaPedido? Resumen, string Mensaje) ObtenerResumenPedido(int idPedido, int idUsuario);

        PosventaUsuario ObtenerDeUsuario(int idUsuario);

        // HU-19
        (Devolucion? Devolucion, string Mensaje) SolicitarDevolucion(int idUsuario, SolicitudDevolucion solicitud);

        // HU-21
        (List<Devolucion>? Devoluciones, string Mensaje) ObtenerDevoluciones(int idAdministrador);

        (Devolucion? Devolucion, string Mensaje) CambiarEstadoDevolucion(
            int idDevolucion, int idAdministrador, string accion, string? motivo, string? referencia);

        // HU-22
        (TicketGarantia? Ticket, string Mensaje) ReportarGarantia(
            int idUsuario, int idPedido, int idProducto, int cantidad, string descripcion, List<ArchivoEvidencia> fotos);

        // HU-23
        (List<TicketGarantia>? Garantias, string Mensaje) ObtenerGarantias(int idAdministrador);

        (TicketGarantia? Ticket, string Mensaje) CambiarEstadoGarantia(
            int idTicket, int idAdministrador, string accion, string? motivo, string? cubiertoPor, int? idProductoReemplazo);

        (byte[]? Contenido, string TipoContenido, string Mensaje) ObtenerEvidencia(int idTicket, int idUsuario, string archivo);
    }
}
