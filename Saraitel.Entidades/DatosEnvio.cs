using System;

namespace Saraitel.Entidades
{
    // Datos de entrega (o de facturación) de un pedido.
    // Se guardan en el pedido y no dependen del perfil del cliente.
    public class DatosEnvio
    {
        public string Pais { get; set; } = "Colombia";

        public string Nombre { get; set; } = string.Empty;

        public string Apellidos { get; set; } = string.Empty;

        public string Cedula { get; set; } = string.Empty;

        public string Direccion { get; set; } = string.Empty;

        public string Complemento { get; set; } = string.Empty;

        public string Ciudad { get; set; } = string.Empty;

        public string Departamento { get; set; } = string.Empty;

        public string CodigoPostal { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;
    }
}
