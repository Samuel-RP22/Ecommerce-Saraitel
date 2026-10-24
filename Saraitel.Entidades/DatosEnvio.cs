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

        // Tipo de documento: CC, CE, TI, PP (pasaporte) o NIT.
        // Los pedidos anteriores a este campo quedan como cédula (CC).
        public string TipoDocumento { get; set; } = "CC";

        // Número del documento (se mantiene el nombre por compatibilidad
        // con los pedidos ya guardados)
        public string Cedula { get; set; } = string.Empty;

        public string Direccion { get; set; } = string.Empty;

        public string Complemento { get; set; } = string.Empty;

        public string Ciudad { get; set; } = string.Empty;

        public string Departamento { get; set; } = string.Empty;

        public string CodigoPostal { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        /* ---------- Tipos de documento ---------- */

        public static readonly string[] TiposDocumento = { "CC", "CE", "TI", "PP", "NIT" };

        // Cómo se muestra el tipo en pantalla y en la factura
        public static string Abreviatura(string? tipo)
        {
            return tipo switch
            {
                "CE" => "C.E.",
                "TI" => "T.I.",
                "PP" => "Pasaporte",
                "NIT" => "NIT",
                _ => "C.C."
            };
        }

        // El pasaporte admite letras; los demás documentos solo números
        public static string NormalizarNumero(string? tipo, string? numero)
        {
            var permitidos = (numero ?? "").Where(c => tipo == "PP" ? char.IsLetterOrDigit(c) : char.IsDigit(c));

            return new string(permitidos.ToArray()).ToUpperInvariant();
        }

        // Devuelve el error del número según el tipo, o null si es válido
        public static string? ValidarNumero(string tipo, string numero)
        {
            var (minimo, maximo, texto) = tipo switch
            {
                "CC" => (6, 10, "números"),
                "CE" => (5, 12, "números"),
                "TI" => (8, 11, "números"),
                "NIT" => (9, 10, "números"),
                _ => (5, 15, "letras o números")
            };

            if (numero.Length < minimo || numero.Length > maximo)
                return $"el número de documento debe tener entre {minimo} y {maximo} {texto}";

            return null;
        }
    }
}
