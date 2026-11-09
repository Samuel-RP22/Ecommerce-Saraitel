using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;
using Saraitel.Entidades.Factories;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace Saraitel.API.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly
            UsuarioRepository _usuarioRepository;

        // Intentos fallidos de inicio de sesión, contados POR CORREO (antes era un
        // solo número para todos, que mezclaba los intentos de cuentas distintas).
        // Se guardan en memoria; al reiniciar la API se borran.
        private const int MaxIntentos = 3;
        private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);
        private static readonly ConcurrentDictionary<string, IntentosLogin> _intentosPorCorreo = new();

        private class IntentosLogin
        {
            public int Fallidos;
            public DateTime? BloqueadoHasta;
        }

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

            // La base de datos busca el correo sin importar mayúsculas
            return _usuarioRepository.ObtenerUsuarioPorCorreo(correo);
        }

        public string RegistrarUsuario(string rol,string nombre,string correo,string telefono,string contrasena,string direccion = "")
        {
            // Si un campo no llega en la petición viene como null: se trata como vacío
            nombre = (nombre ?? "").Trim();
            correo = (correo ?? "").Trim();
            telefono = (telefono ?? "").Trim();
            direccion = (direccion ?? "").Trim();
            contrasena ??= "";

            // Todos los campos del registro son obligatorios.
            // Esta validación es la que manda: la del navegador se puede saltar
            // (por ejemplo, llamando la API desde Swagger o Postman)
            if (nombre == "")
                return "El nombre es obligatorio";

            if (correo == "")
                return "El correo es obligatorio";

            if (!EsCorreoValido(correo))
                return "El correo no es válido";

            if (telefono == "")
                return "El teléfono es obligatorio";

            if (!EsTelefonoValido(telefono))
                return "El teléfono debe tener entre 7 y 15 dígitos";

            if (string.IsNullOrWhiteSpace(contrasena))
                return "La contraseña es obligatoria";

            if (contrasena.Length < 6)
                return "La contraseña debe tener al menos 6 caracteres";

            if (direccion == "")
                return "La dirección es obligatoria";

            var usuarioExistente = _usuarioRepository.ObtenerUsuarioPorCorreo(correo);

            if (usuarioExistente != null)
            {
                return
                    "El correo ya está registrado";
            }

            if (_usuarioRepository.ExisteTelefono(telefono))
                return "El teléfono ya está registrado";

            // El id lo asigna la base de datos al guardar (1, 2, 3...).
            // La contraseña se guarda cifrada con BCrypt, nunca en texto plano
            Usuario usuario = UsuarioFactory.CrearUsuario(
                    rol,
                    0,
                    nombre,
                    correo,
                    telefono,
                    CifrarContrasena(contrasena),
                    direccion);

            try
            {
                _usuarioRepository.AgregarUsuario(usuario);
            }
            catch (CorreoDuplicadoException)
            {
                // Dos registros con el mismo correo al mismo tiempo: la base de datos
                // frena el segundo y aquí se convierte en un mensaje claro
                return "El correo ya está registrado";
            }
            catch (TelefonoDuplicadoException)
            {
                return "El teléfono ya está registrado";
            }

            return
                "Usuario registrado correctamente";
        }

        public string IniciarSesion(string correo, string contrasena)
        {
            string clave = (correo ?? "").Trim().ToLower();
            var intentos = _intentosPorCorreo.GetOrAdd(clave, _ => new IntentosLogin());

            // ¿La cuenta sigue bloqueada por intentos anteriores?
            if (intentos.BloqueadoHasta != null && intentos.BloqueadoHasta > DateTime.UtcNow)
            {
                int minutos = (int)Math.Ceiling((intentos.BloqueadoHasta.Value - DateTime.UtcNow).TotalMinutes);
                return $"Demasiados intentos fallidos. Intenta de nuevo en {minutos} minuto(s).";
            }

            var usuario = ObtenerUsuarioPorCorreo(correo);

            // Mismo mensaje si el correo no existe o si la contraseña está mal:
            // así nadie puede averiguar qué correos están registrados probando el login
            if (usuario == null || !ContrasenaCorrecta(contrasena, usuario.Contrasena))
            {
                intentos.Fallidos++;
                int restantes = MaxIntentos - intentos.Fallidos;

                if (restantes <= 0)
                {
                    // Se agotaron los intentos: se bloquea por un rato y se reinicia el contador
                    intentos.BloqueadoHasta = DateTime.UtcNow.Add(DuracionBloqueo);
                    intentos.Fallidos = 0;

                    return "El correo o la contraseña que se ingresaron son incorrectos.\n" +
                           $"Has superado los {MaxIntentos} intentos. Intenta de nuevo en {(int)DuracionBloqueo.TotalMinutes} minutos.";
                }

                string textoIntentos = restantes == 1 ? "1 intento restante" : $"{restantes} intentos restantes";

                return "El correo o la contraseña que se ingresaron son incorrectos.\n" +
                       $"Tienes {textoIntentos}.";
            }

            // Entró bien: se olvidan los intentos fallidos de este correo
            _intentosPorCorreo.TryRemove(clave, out _);

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

            if (telefono != "" && !EsTelefonoValido(telefono))
                return "El teléfono debe tener entre 7 y 15 dígitos";

            var otroUsuario = _usuarioRepository.ObtenerUsuarioPorCorreo(correo);

            if (otroUsuario != null && otroUsuario.IdUsuario != idUsuario)
                return "El correo ya está registrado por otro usuario";

            if (_usuarioRepository.ExisteTelefono(telefono, idUsuario))
                return "El teléfono ya está registrado por otro usuario";

            usuario.Nombre = nombre;
            usuario.Correo = correo;
            usuario.Telefono = telefono;

            if (usuario is Cliente cliente)
                cliente.Direccion = direccion;

            try
            {
                _usuarioRepository.ActualizarUsuario(usuario);
            }
            catch (CorreoDuplicadoException)
            {
                return "El correo ya está registrado por otro usuario";
            }
            catch (TelefonoDuplicadoException)
            {
                return "El teléfono ya está registrado por otro usuario";
            }

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

            usuario.Contrasena = CifrarContrasena(nuevaContrasena);

            // El código es de un solo uso
            usuario.TokenRecuperacion = null;
            usuario.FechaExpiracionToken = null;

            _usuarioRepository.ActualizarUsuario(usuario);

            return "Contraseña restablecida correctamente";
        }

        /* =========================
           CONTRASEÑAS (BCrypt)
        ========================= */

        // BCrypt agrega una "sal" aleatoria: la misma contraseña nunca queda igual dos veces.
        // Costo 10: seguro y rápido (el 11 por defecto tarda el doble en cada inicio de sesión)
        private static string CifrarContrasena(string contrasena)
        {
            return BCrypt.Net.BCrypt.HashPassword(contrasena ?? "", 10);
        }

        private static bool ContrasenaCorrecta(string contrasena, string guardada)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(contrasena ?? "", guardada);
            }
            catch (Exception)
            {
                // Lo guardado no es un cifrado BCrypt válido
                return false;
            }
        }

        // Solo dígitos, entre 7 y 15
        private static bool EsTelefonoValido(string telefono)
        {
            return telefono.Length >= 7 && telefono.Length <= 15 &&
                   telefono.All(c => c >= '0' && c <= '9');
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