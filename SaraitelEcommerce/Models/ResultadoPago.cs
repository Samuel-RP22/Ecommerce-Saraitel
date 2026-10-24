using Saraitel.Entidades;

namespace Saraitel.API.Models
{
    // Resultado de confirmar un pago con Wompi (HU-16)
    public class ResultadoPago
    {
        public const string Aprobado = "APROBADO";
        public const string Pendiente = "PENDIENTE";
        public const string Rechazado = "RECHAZADO";
        public const string Error = "ERROR";

        public string Estado { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;

        public Pedido? Pedido { get; set; }

        // HU-17: correo al que se envió la factura (vacío si no se pudo enviar)
        public string CorreoFactura { get; set; } = string.Empty;
    }
}
