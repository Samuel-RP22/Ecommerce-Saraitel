namespace Saraitel.Datos.Repositories
{
    // Se lanza cuando la base de datos rechaza un correo que ya usa otra cuenta
    // (índice único ux_usuarios_correo). Así el servicio responde con un mensaje
    // claro en vez de que la API se caiga con una PostgresException.
    public class CorreoDuplicadoException : Exception
    {
        public CorreoDuplicadoException(Exception interna)
            : base("El correo ya está registrado", interna)
        {
        }
    }

    // Igual, pero para el teléfono (índice único ux_usuarios_telefono)
    public class TelefonoDuplicadoException : Exception
    {
        public TelefonoDuplicadoException(Exception interna)
            : base("El teléfono ya está registrado", interna)
        {
        }
    }
}
