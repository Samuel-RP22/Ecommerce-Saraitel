using Saraitel.Entidades;

namespace Saraitel.Entidades.Factories
{
    public static class UsuarioFactory
    {
        public static Usuario CrearUsuario(
            string rol,
            int idUsuario,
            string nombre,
            string correo,
            string telefono,
            string contrasena,
            string direccion = "")
        {
            rol = rol.ToLower();

            if (rol == "cliente")
            {
                return new Cliente
                {
                    IdUsuario = idUsuario,
                    Nombre = nombre,
                    Correo = correo,
                    Telefono = telefono,
                    Contrasena = contrasena,
                    Direccion = direccion
                };
            }

            if (rol == "administrador")
            {
                return new Administrador
                {
                    IdUsuario = idUsuario,
                    Nombre = nombre,
                    Correo = correo,
                    Telefono = telefono,
                    Contrasena = contrasena
                };
            }
            throw new Exception("Rol inválido");
        }
    }
}