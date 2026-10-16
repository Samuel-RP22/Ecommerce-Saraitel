using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Saraitel.Entidades
{
    [JsonPolymorphic(
    TypeDiscriminatorPropertyName = "tipo")]

    [JsonDerivedType(
    typeof(Cliente),
    "Cliente")]

    [JsonDerivedType(
    typeof(Administrador),
    "Administrador")]

    public abstract class Usuario
    {
        public int IdUsuario { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Correo { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Contrasena { get; set; } = string.Empty;

        public string Rol { get; set; } = string.Empty;

        // HU-06 / HU-07: recuperación de acceso
        public string? TokenRecuperacion { get; set; }

        public DateTime? FechaExpiracionToken { get; set; }
    }
}