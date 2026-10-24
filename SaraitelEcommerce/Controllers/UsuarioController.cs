using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
using Saraitel.Entidades;

namespace Saraitel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuarioController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        [HttpPost("login")]
        public IActionResult Login(
            [FromQuery] string correo,
            [FromQuery] string contrasena)
        {
            var resultado = _usuarioService.IniciarSesion(correo, contrasena);

            if (!resultado.StartsWith("Bienvenido"))
                return BadRequest(resultado);

            var usuario = _usuarioService.ObtenerUsuarioPorCorreo(correo);

            return Ok(new
            {
                mensaje = resultado,
                idUsuario = usuario?.IdUsuario ?? 0,
                nombre = usuario?.Nombre ?? "",
                rol = usuario?.Rol ?? "Cliente"
            });
        }

        [HttpPost("registro")]
        public IActionResult RegistrarUsuario(
            string nombre,
            string correo,
            string telefono,
            string contrasena,
            string direccion = "")
        {
            var resultado = _usuarioService.RegistrarUsuario(
                "cliente",
                nombre,
                correo,
                telefono,
                contrasena,
                direccion);

            if (resultado.Contains("correctamente"))
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [HttpGet("correo/{correo}")]
        public IActionResult ObtenerPorCorreo(string correo)
        {
            var usuario = _usuarioService.ObtenerUsuarioPorCorreo(correo);

            if (usuario == null)
                return NotFound("Usuario no encontrado");

            // Sin contraseña ni código de recuperación
            return Ok(new
            {
                idUsuario = usuario.IdUsuario,
                nombre = usuario.Nombre,
                correo = usuario.Correo,
                telefono = usuario.Telefono,
                rol = usuario.Rol,
                historialCompras = usuario is Cliente cliente
                    ? cliente.HistorialCompras
                    : new List<Pedido>()
            });
        }

        [HttpGet]
        public IActionResult ObtenerUsuarios()
        {
            var usuarios = _usuarioService.ObtenerUsuarios()
                .Select(u => new
                {
                    idUsuario = u.IdUsuario,
                    nombre = u.Nombre,
                    correo = u.Correo,
                    telefono = u.Telefono,
                    rol = u.Rol
                });

            return Ok(usuarios);
        }

        [HttpGet("{id:int}")]
        public IActionResult ObtenerPorId(int id)
        {
            var usuario = _usuarioService.ObtenerUsuarioPorId(id);

            if (usuario == null)
                return NotFound("Usuario no encontrado");

            return Ok(new
            {
                idUsuario = usuario.IdUsuario,
                nombre = usuario.Nombre,
                correo = usuario.Correo,
                telefono = usuario.Telefono,
                rol = usuario.Rol
            });
        }

        /* =========================
           HU-04 CONSULTAR PERFIL
        ========================= */

        [HttpGet("perfil/{id:int}")]
        public IActionResult ObtenerPerfil(int id)
        {
            var usuario = _usuarioService.ObtenerUsuarioPorId(id);

            if (usuario == null)
                return NotFound("Usuario no encontrado");

            // Se devuelve solo lo necesario (sin contraseña ni código de recuperación)
            return Ok(new
            {
                idUsuario = usuario.IdUsuario,
                nombre = usuario.Nombre,
                correo = usuario.Correo,
                telefono = usuario.Telefono,
                rol = usuario.Rol,
                direccion = usuario is Cliente cliente ? cliente.Direccion : ""
            });
        }

        /* =========================
           HU-05 ACTUALIZAR PERFIL
        ========================= */

        [HttpPut("perfil/{id:int}")]
        public IActionResult ActualizarPerfil(
            int id,
            string nombre,
            string correo,
            string telefono,
            string direccion = "")
        {
            var resultado = _usuarioService.ActualizarPerfil(
                id,
                nombre,
                correo,
                telefono,
                direccion);

            if (resultado == "Usuario no encontrado")
                return NotFound(resultado);

            if (resultado.Contains("correctamente"))
                return Ok(resultado);

            return BadRequest(resultado);
        }

        /* ==========================================
           HU-06 SOLICITAR RECUPERACIÓN DE ACCESO
        ========================================== */

        [HttpPost("recuperar")]
        public IActionResult SolicitarRecuperacion(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return BadRequest("Debes ingresar el correo");

            var enviado = _usuarioService.SolicitarRecuperacion(correo);

            if (!enviado)
                return StatusCode(500, "No se pudo enviar el correo de recuperación. Intenta de nuevo más tarde.");

            // Siempre se responde lo mismo para no revelar si el correo existe
            return Ok(new
            {
                mensaje = "Si el correo está registrado, te enviamos un código de recuperación."
            });
        }

        /* =========================
           HU-07 RESTABLECER CONTRASEÑA
        ========================= */

        [HttpPost("restablecer")]
        public IActionResult RestablecerContrasena(
            string correo,
            string codigo,
            string nuevaContrasena)
        {
            var resultado = _usuarioService.RestablecerContrasena(
                correo,
                codigo,
                nuevaContrasena);

            if (resultado.Contains("correctamente"))
                return Ok(resultado);

            return BadRequest(resultado);
        }
    }
}