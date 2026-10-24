using Saraitel.API.Models;

namespace Saraitel.API.Interfaces
{
    public interface IWompiService
    {
        string LlavePublica { get; }

        string GenerarFirma(string referencia, long montoEnCentavos, string moneda, string fechaLimite);

        Task<TransaccionWompi?> ConsultarTransaccionAsync(string idTransaccion);
    }
}
