using Npgsql;

namespace Saraitel.Datos.Repositories
{
    // Se lanza cuando la base de datos rechaza un dato por sus reglas
    // (CHECK, NOT NULL, texto demasiado largo, número fuera de rango).
    // El servicio la atrapa y responde con un mensaje claro en vez de
    // dejar que la API se caiga con una PostgresException.
    public class DatosInvalidosException : Exception
    {
        public DatosInvalidosException(string mensaje, Exception interna)
            : base(mensaje, interna)
        {
        }

        // Traduce los errores de datos de PostgreSQL; devuelve null si es otro tipo de error
        internal static DatosInvalidosException? Desde(PostgresException ex)
        {
            string? mensaje = ex.SqlState switch
            {
                PostgresErrorCodes.CheckViolation => "Algún dato no cumple las reglas de la tienda (revisa que el precio sea mayor que cero y el stock no sea negativo)",
                PostgresErrorCodes.NotNullViolation => "Falta un dato obligatorio",
                PostgresErrorCodes.StringDataRightTruncation => "Uno de los textos es demasiado largo",
                PostgresErrorCodes.NumericValueOutOfRange => "Uno de los números es demasiado grande",
                _ => null
            };

            return mensaje == null ? null : new DatosInvalidosException(mensaje, ex);
        }
    }
}
