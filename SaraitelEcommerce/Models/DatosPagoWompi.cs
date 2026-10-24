namespace Saraitel.API.Models
{
    // Lo que necesita el widget de Wompi para abrir el pago de un pedido (HU-16)
    public class DatosPagoWompi
    {
        public int IdPedido { get; set; }

        public string LlavePublica { get; set; } = string.Empty;

        public string Referencia { get; set; } = string.Empty;

        public long MontoEnCentavos { get; set; }

        public string Moneda { get; set; } = "COP";

        // Firma de integridad calculada en el servidor con el secreto
        public string Firma { get; set; } = string.Empty;

        // Hora límite en formato ISO 8601 UTC, igual a la usada en la firma
        public string FechaLimite { get; set; } = string.Empty;
    }
}
