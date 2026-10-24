using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;
using Saraitel.Entidades.Factories;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace Saraitel.API.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly
            UsuarioRepository _usuarioRepository;

        private static int
            _intentosFallidos = 0;

        private readonly IEmailService _emailService;

        public UsuarioService(IEmailService emailService)
        {
            _usuarioRepository = new UsuarioRepository();
            _emailService = emailService;
        }

        public List<Usuario>
            ObtenerUsuarios()
        {
            return _usuarioRepository.ObtenerUsuarios();
        }

        public Usuario?
            ObtenerUsuarioPorId(int id)
        {
            return _usuarioRepository.ObtenerUsuarioPorId(id);
        }

        public Usuario? ObtenerUsuarioPorCorreo(string correo)
        {
            if (string.IsNullOrEmpty(correo)) return null;
            return ObtenerUsuarios().FirstOrDefault(u => u.Correo.Trim().ToLower() == correo.Trim().ToLower());
        }

        public string RegistrarUsuario(string rol,string nombre,string correo,string telefono,string contrasena,string direccion = "")
        {
            var usuarioExistente = _usuarioRepository.ObtenerUsuarioPorCorreo(correo);

            if (usuarioExistente!= null)
            {
                return
                    "El correo ya está registrado";
            }

            int nuevoId =ObtenerUsuarios().Any()? 
                ObtenerUsuarios().Max(u =>u.IdUsuario) + 1: 1;

            Usuario usuario = UsuarioFactory.CrearUsuario(
                    rol,
                    nuevoId,
                    nombre,
                    correo,
                    telefono,
                    contrasena,
                    direccion);
            _usuarioRepository.AgregarUsuario(usuario);

            return
                "Usuario registrado correctamente";
        }

        public string IniciarSesion(string correo, string contrasena)
        {
            var usuario = ObtenerUsuarioPorCorreo(correo);
            if (usuario == null)
            {
                return "Usuario no encontrado";
            }
            if (usuario.Contrasena != contrasena)
            {
                _intentosFallidos++;

                return $"Contrasena incorrecta. Intentos restantes: {3 - _intentosFallidos}";
            }
            _intentosFallidos = 0;
            return $"Bienvenido {usuario.Nombre} ({usuario.Rol})";
        }

        /* =========================
           HU-05 ACTUALIZAR PERFIL
        ========================= */

        public string ActualizarPerfil(int idUsuario, string nombre, string correo, string telefono, string direccion = "")
        {
            var usuario = _usuarioRepository.ObtenerUsuarioPorId(idUsuario);

            if (usuario == null)
                return "Usuario no encontrado";

            nombre = (nombre ?? "").Trim();
            correo = (correo ?? "").Trim();
            telefono = (telefono ?? "").Trim();
            direccion = (direccion ?? "").Trim();

            if (nombre == "")
                return "El nombre es obligatorio";

            if (!EsCorreoValido(correo))
                return "El correo no es válido";

            if (telefono != "" &&
                (telefono.Length < 7 || telefono.Length > 15 ||
                 !telefono.All(c => c >= '0' && c <= '9')))
                return "El teléfono debe tener entre 7 y 15 dígitos";

            var otroUsuario = _usuarioRepository.ObtenerUsuarioPorCorreo(correo);

            if (otroUsuario != null && otroUsuario.IdUsuario != idUsuario)
                return "El correo ya está registrado por otro usuario";

            usuario.Nombre = nombre;
            usuario.Correo = correo;
            usuario.Telefono = telefono;

            if (usuario is Cliente cliente)
                cliente.Direccion = direccion;

            _usuarioRepository.ActualizarUsuario(usuario);

            return "Perfil actualizado correctamente";
        }

        /* ==========================================
           HU-06 SOLICITAR RECUPERACIÓN DE ACCESO
        ========================================== */

        public bool SolicitarRecuperacion(string correo)
        {
            var usuario = _usuarioRepository.ObtenerUsuarioPorCorreo(correo);

            // Si el correo no existe no se hace nada, pero se responde igual
            // para no revelar qué correos están registrados
            if (usuario == null)
                return true;

            // Código numérico de 6 dígitos, válido por 15 minutos
            string codigo = RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();

            usuario.TokenRecuperacion = codigo;
            usuario.FechaExpiracionToken = DateTime.UtcNow.AddMinutes(15);

            _usuarioRepository.ActualizarUsuario(usuario);

            string cuerpo = $@"
                <div style='font-family:Arial,sans-serif;max-width:480px;margin:auto;'>
                    <h2>Recuperación de contraseña</h2>
                    <p>Hola {WebUtility.HtmlEncode(usuario.Nombre)},</p>
                    <p>Usa este código para restablecer tu contraseña en Saraitel:</p>
                    <p style='font-size:32px;font-weight:bold;letter-spacing:6px;'>{codigo}</p>
                    <p>El código vence en 15 minutos y solo se puede usar una vez.</p>
                    <p style='color:#666;font-size:13px;'>Si no fuiste tú, ignora este mensaje.</p>
                </div>";

            try
            {
                _emailService.EnviarCorreo(
                    usuario.Correo,
                    "Código de recuperación - Saraitel",
                    cuerpo);

                return true;
            }
            catch (Exception ex)
            {
                // Si no se pudo enviar, el código no sirve: lo borramos
                Console.WriteLine($"Error enviando correo de recuperación: {ex}");
                System.Diagnostics.Debug.WriteLine($"Error enviando correo de recuperación: {ex}");

                usuario.TokenRecuperacion = null;
                usuario.FechaExpiracionToken = null;
                _usuarioRepository.ActualizarUsuario(usuario);

                return false;
            }
        }

        /* =========================
           HU-07 RESTABLECER CONTRASEÑA
        ========================= */

        public string RestablecerContrasena(string correo, string codigo, string nuevaContrasena)
        {
            if (string.IsNullOrWhiteSpace(nuevaContrasena) || nuevaContrasena.Length < 6)
                return "La nueva contraseña debe tener al menos 6 caracteres";

            var usuario = _usuarioRepository.ObtenerUsuarioPorCorreo(correo);

            if (usuario == null ||
                string.IsNullOrEmpty(usuario.TokenRecuperacion) ||
                usuario.FechaExpiracionToken == null ||
                usuario.FechaExpiracionToken < DateTime.UtcNow ||
                usuario.TokenRecuperacion != (codigo ?? "").Trim())
            {
                return "Código inválido o expirado";
            }

            usuario.Contrasena = nuevaContrasena;

            // El código es de un solo uso
            usuario.TokenRecuperacion = null;
            usuario.FechaExpiracionToken = null;

            _usuarioRepository.ActualizarUsuario(usuario);

            return "Contraseña restablecida correctamente";
        }

        private static bool EsCorreoValido(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return false;

            return MailAddress.TryCreate(correo, out var direccion)
                   && direccion.Address == correo;
        }
    }
}