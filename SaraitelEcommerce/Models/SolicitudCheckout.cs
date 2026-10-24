using Saraitel.Entidades;

namespace Saraitel.API.Models
{
    // Lo que envía el formulario de checkout (HU-15)
    public class SolicitudCheckout
    {
        public DatosEnvio Envio { get; set; } = new DatosEnvio();

        // Si es true, la factura usa la misma dirección de envío
        public bool FacturacionIgualEnvio { get; set; } = true;

        public DatosEnvio? Facturacion { get; set; }
    }
}
