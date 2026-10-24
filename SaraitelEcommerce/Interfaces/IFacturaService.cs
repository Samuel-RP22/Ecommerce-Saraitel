using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface IFacturaService
    {
        byte[] GenerarPdf(Pedido pedido, string correoCliente);
    }
}
