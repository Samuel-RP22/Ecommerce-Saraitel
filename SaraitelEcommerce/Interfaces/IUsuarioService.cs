using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface IUsuarioService
    {
        List<Usuario> ObtenerUsuarios();

        Usuario? ObtenerUsuarioPorId(int id);

        Usuario? ObtenerUsuarioPorCorreo(string correo);

        string RegistrarUsuario(
            string rol,
            string nombre,
            string correo,
            string telefono,
            string Contrasena,
            string direccion = "");

        string IniciarSesion(
            string correo,
            string Contrasena);

        // HU-05
        string ActualizarPerfil(
            int idUsuario,
            string nombre,
            string correo,
            string telefono,
            string direccion = "");

        // HU-06: devuelve el código generado, o null si el correo no existe
        string? SolicitarRecuperacion(string correo);

        // HU-07
        string RestablecerContrasena(
            string correo,
            string codigo,
            string nuevaContrasena);
    }
}